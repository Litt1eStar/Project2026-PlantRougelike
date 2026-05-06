using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 5f;
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        float xInput = Input.GetAxisRaw("Horizontal");
        float yInput = Input.GetAxisRaw("Vertical");
        Vector3 movement = new Vector3(xInput, 0f, yInput).normalized * Time.deltaTime * moveSpeed;
        transform.Translate(movement);
    }
}
