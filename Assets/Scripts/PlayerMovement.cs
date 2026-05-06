using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    [SerializeField] private float walkSpeed = 2f;
    [SerializeField] private float runSpeed = 5f;

    private float movementSpeed;
    private void Awake()
    {
        movementSpeed = walkSpeed;
    }   
    void Start()
    {
        
    }

    void Update()
    {
        InputDetection(out float xInput, out float yInput);
        MovementSpeedAdjustment();
        Move(xInput, yInput);
    }
    private void InputDetection(out float xInput, out float yInput)
    {
        xInput = Input.GetAxisRaw("Horizontal");
        yInput = Input.GetAxisRaw("Vertical");
    }
    private void MovementSpeedAdjustment()
    {
       if (Input.GetKey(KeyCode.LeftShift))
       {
           movementSpeed = runSpeed;
       }
       else
       {
           movementSpeed = walkSpeed;
       }
    }
    private void Move(float xInput, float yInput) 
    {

        Vector3 movement = new Vector3(xInput, 0f, yInput).normalized * Time.deltaTime * movementSpeed;
        transform.Translate(movement);
    }
}
