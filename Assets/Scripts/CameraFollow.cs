using UnityEngine;

[ExecuteAlways]
public class CameraFollow : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private float smoothSpeed = 5f;
    [SerializeField] private Vector3 offset = new Vector3(0f, 10f, -5f);

    private void LateUpdate()
    {
        if (target == null) return;

        Vector3 desired = target.position + offset;

        if (Application.isPlaying)
            transform.position = Vector3.Lerp(transform.position, desired, smoothSpeed * Time.deltaTime);
        else
            transform.position = desired;
    }
}