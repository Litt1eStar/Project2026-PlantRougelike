using UnityEngine;
using UnityEditor;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace AIIntegration
{
    public class AIProjectAssistant : EditorWindow
    {
        private string userPrompt = "";
        private string aiResponse = "";
        private Vector2 scrollPos;
        private bool isProcessing = false;
        private string selectedScript = "ask_gemini.sh";

        [MenuItem("Window/AI Project Assistant")]
        public static void ShowWindow()
        {
            GetWindow<AIProjectAssistant>("AI Assistant");
        }

        private void OnGUI()
        {
            GUILayout.Label("AI Project Assistant", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Model:", GUILayout.Width(50));
            if (GUILayout.Toggle(selectedScript == "ask_gemini.sh", "Gemini", "Button")) selectedScript = "ask_gemini.sh";
            if (GUILayout.Toggle(selectedScript == "ask_claude.sh", "Claude", "Button")) selectedScript = "ask_claude.sh";
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Prompt:");
            userPrompt = EditorGUILayout.TextArea(userPrompt, EditorStyles.textArea, GUILayout.Height(100));

            EditorGUILayout.Space();
            GUI.enabled = !isProcessing && !string.IsNullOrWhiteSpace(userPrompt);
            if (GUILayout.Button("Send Request", GUILayout.Height(30)))
            {
                AskAI(selectedScript);
            }
            GUI.enabled = true;

            if (isProcessing)
            {
                EditorGUILayout.HelpBox("Processing request... Please wait.", MessageType.Info);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Response:");
            
            // Using a text area within a scroll view for the response
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos, GUILayout.ExpandHeight(true));
            EditorStyles.label.wordWrap = true;
            EditorGUILayout.SelectableLabel(aiResponse, EditorStyles.wordWrappedLabel, GUILayout.MinHeight(200), GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();

            if (GUILayout.Button("Clear", GUILayout.Width(60)))
            {
                aiResponse = "";
                userPrompt = "";
                Repaint();
            }
        }

        private void AskAI(string scriptName)
        {
            isProcessing = true;
            aiResponse = "Waiting for response...";
            Repaint();

            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            string scriptPath = Path.Combine(projectRoot, scriptName);

            if (!File.Exists(scriptPath))
            {
                aiResponse = $"Error: Script not found at {scriptPath}";
                isProcessing = false;
                Repaint();
                return;
            }

            // Execute in background thread to avoid freezing Unity UI
            Task.Run(() => {
                string result = ExecuteBashScript(scriptPath, userPrompt, projectRoot);
                
                // Return to main thread to update UI
                EditorApplication.delayCall += () => {
                    aiResponse = result;
                    isProcessing = false;
                    Repaint();
                };
            });
        }

        private string ExecuteBashScript(string scriptPath, string prompt, string workingDir)
        {
            try
            {
                // Escape prompt for shell usage
                string escapedPrompt = prompt.Replace("\"", "\\\"");

                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = "bash",
                    Arguments = $"\"{scriptPath}\" \"{escapedPrompt}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = workingDir
                };

                using (Process process = Process.Start(startInfo))
                {
                    string output = process.StandardOutput.ReadToEnd();
                    string error = process.StandardError.ReadToEnd();
                    process.WaitForExit();

                    if (!string.IsNullOrWhiteSpace(error))
                    {
                        // Some scripts might output info to stderr that isn't necessarily a fatal error,
                        // but usually it's worth showing.
                        return $"Output:\n{output}\n\nError/Info:\n{error}";
                    }
                    return output;
                }
            }
            catch (System.Exception e)
            {
                return $"Exception occurred: {e.Message}\nCheck if 'bash' is installed and in your PATH.";
            }
        }
    }
}
