using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    [SerializeField] private float walkSpeed = 2f;
    [SerializeField] private float runSpeed = 5f;

    private Transform animTransform;
    private Animator anim;
    private float movementSpeed;
    private bool isWalk;
    private int direction; //0 = front, 1 = side, 2 = back
    private void Awake()
    {
        anim = GetComponentInChildren<Animator>();
        if(anim == null)
        {
            Debug.LogError("Animator component not found in children.");
        }

        animTransform = GetComponentInChildren<Animator>().transform;
        if(animTransform == null)
        {
            Debug.LogError("Animator Transform component not found in children.");
        }

        movementSpeed = walkSpeed;
        isWalk = false;
        direction = 0;
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

        if(Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow))
        {
            direction = 1;
            animTransform.localScale = new Vector3(1, 1, 1); // Face right
        }
        else if(Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))
        {
            direction = 1;
            animTransform.localScale = new Vector3(-1, 1, 1); // Face left
        }
        else if(Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))
        {
            direction = 2;
        }
        else if(Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))
        {
            direction = 0;
        }   

        isWalk = xInput != 0 || yInput != 0;
        anim.SetBool("isWalk", isWalk);
        anim.SetInteger("direction", direction);
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
