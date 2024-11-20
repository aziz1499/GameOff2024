using System.Collections;
using System.Collections.Generic;
using Cinemachine;
using UnityEngine;
using UnityEngine.Scripting;

[DefaultExecutionOrder(-1)]
public class PlayerController : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private CharacterController characterController;
    [SerializeField] private CinemachineFreeLook mainVCam;
    [SerializeField] private Camera playerCamera;
    [SerializeField] private Animator playerAnim;
    private UIScripts ui;


    [Header("Movement")]
    [SerializeField] private bool canMove = false;
    public float runAcceleration;
    public float drag;
    public float runSpeed;
    [SerializeField] float sprintMult;

    [Header("Vertical Movement")]
    private float gravity = -9.81f;
    [SerializeField] float gravMult = 3;
    private float verticalVelocity = 0;
    [SerializeField] float jumpStrength;
    private float coyoteTime;
    private float coyoteThreshold = 1.0f;

    private Vector3 movementToApply;

    [Header("Abilities")]
    [SerializeField] float flashCooldown = 1;
    private float flashCooldownTimer;
    public int disguisesOwned = 1;
    public bool isTryingToHide = true;

    [Header("Auto-Movement")]
    public bool isAutomoving = false;
    public List<Vector3> autoDestinationQueue = new List<Vector3>();
    private float autoDestTimeout = 0;//time limit for auto-movement

    [Header("Camera")]
    public float turnSmoothTime = 0.1f;
    private float turnSmoothVelocity;

    private PlayerLocomotionInput playerLocomotionInput;
    

    private void Awake()
    {
        playerLocomotionInput = GetComponent<PlayerLocomotionInput>();
        ui = FindObjectOfType<UIScripts>();
    }

    private void Update()
    {
        movementToApply = new Vector3(0, 0, 0);//zero movement
        //pause game
        if(playerLocomotionInput.PausePressed)
        {
            ui.TogglePause();
            mainVCam.enabled = !mainVCam.enabled;
        }
        if(mainVCam.enabled)//if not paused
        {
            if(!isAutomoving)//if player is controlling movement
            {
                HandleGravity();
                HandleJump();
                if(canMove)
                {
                    HandleCameraAndMovement();
                }
                HandleFlash();

                //set animation parameters
                playerAnim.SetBool("isWalking", (playerLocomotionInput.MovementInput.magnitude > 0.1f));
                playerAnim.SetBool("isSprinting", (playerLocomotionInput.SprintInput > 0.1f));
            }
            else//if automatic movement
            {        
                HandleGravity();
                HandleJump();
                if(canMove)
                {
                    HandleAutoMovement();
                }
                playerAnim.SetBool("isWalking", (autoDestinationQueue.Count >= 1));
                playerAnim.SetBool("isSprinting", false);
            }
            if(verticalVelocity < -10)//if falling
            {
                if(!playerAnim.GetBool("isFalling"))
                {
                    playerAnim.SetBool("isFalling", true);
                    playerAnim.SetBool("isJumping", false);
                }
            }
        }
    }

    //calculate camera after movement
    private void LateUpdate()
    {
        if(mainVCam.enabled)//if not paused
        {
            if(!isAutomoving)//if player is controlling movement
            {
                //when player moving
                if(playerLocomotionInput.MovementInput.magnitude > 0.1f)
                {
                    Vector3 inpDirection = new Vector3(playerLocomotionInput.MovementInput.x, 0, playerLocomotionInput.MovementInput.y).normalized;

                    //set the rotation to facing direction
                    float targAng = Mathf.Atan2(inpDirection.x, inpDirection.z) * Mathf.Rad2Deg + playerCamera.transform.eulerAngles.y;
                    float smoothedAngle = Mathf.SmoothDampAngle(transform.eulerAngles.y, targAng, ref turnSmoothVelocity, turnSmoothTime);
                    transform.rotation = Quaternion.Euler(0f, smoothedAngle, 0f);
                }
            }
            else
            {
                if(autoDestinationQueue.Count >= 1)
                {
                    Vector3 inpDirection = (new Vector3((autoDestinationQueue[0].x - transform.position.x), 0, (autoDestinationQueue[0].z - transform.position.z))).normalized;

                    //set the rotation to facing direction
                    float targAng = Mathf.Atan2(inpDirection.x, inpDirection.z) * Mathf.Rad2Deg;
                    transform.rotation = Quaternion.Euler(0f, targAng, 0f);
                }
            }
        }
    }

    private void HandleGravity()//calculate downward force
    {
        if(characterController.isGrounded && verticalVelocity < 0)//if on the floor and moving downwards
        {
            verticalVelocity = -1;
            playerAnim.SetBool("isFalling", false);
        }
        else
        {
            verticalVelocity += gravity * gravMult * Time.deltaTime;
        }
    }

    private void HandleCameraAndMovement()
    {
        //calculate move forward in camera direction
        Vector3 cameraForwardXZ = new Vector3(playerCamera.transform.forward.x, 0, playerCamera.transform.forward.z).normalized;
        Vector3 cameraRightXZ = new Vector3(playerCamera.transform.right.x, 0, playerCamera.transform.right.z).normalized;
        Vector3 moveDirection = cameraRightXZ * playerLocomotionInput.MovementInput.x + cameraForwardXZ * playerLocomotionInput.MovementInput.y;

        Vector3 moveDelta = moveDirection * runAcceleration * Time.deltaTime * ((playerLocomotionInput.SprintInput > 0.1f) ? sprintMult : 1);//if sprinting, multiply acceleration
        Vector3 newVel = characterController.velocity + moveDelta;
        newVel.y = 0;//no vertical speed limit

        //apply drag
        Vector3 curDrag = newVel.normalized * drag * Time.deltaTime;
        newVel = (newVel.magnitude > drag * Time.deltaTime) ? newVel - curDrag : Vector3.zero;
        newVel = (playerLocomotionInput.SprintInput > 0.1f) ? Vector3.ClampMagnitude(newVel, runSpeed * sprintMult) : Vector3.ClampMagnitude(newVel, runSpeed);//if sprinting, multiply speed when clamping

        //apply movement
        movementToApply += newVel;
        movementToApply.y += verticalVelocity;//and gravity and jump
        //move player
        characterController.Move(movementToApply * Time.deltaTime);
    }

    private void HandleJump()
    {
        //coyote time check
        if(characterController.isGrounded)
        {
            coyoteTime = 0;
        }
        else
        {
            coyoteTime += Time.deltaTime;
        }
        //jump
        if(playerLocomotionInput.JumpPressed)
        {
            if(coyoteTime <= coyoteThreshold)//if on ground or in coyote time
            {
                coyoteTime = 10;
                verticalVelocity = jumpStrength;
                playerAnim.SetBool("isFalling", false);
                playerAnim.SetBool("isJumping", true);
            }
        }
    }

    private void HandleFlash()
    {
        flashCooldownTimer -= Time.deltaTime;
        //set visual cooldown
        ui.abilityFlashImage.fillAmount = flashCooldownTimer / flashCooldown;//ToDo ensure flashcooldown is never 0
        //if flash pressed
        if(playerLocomotionInput.FlashPressed)
        {
            if(flashCooldownTimer <= 0)
            {
                playerAnim.SetTrigger("Flash");
                flashCooldownTimer = flashCooldown;
                ui.HudButtonPressPulse(ui.abilityFlashBox);//ability press animation
            }
            else//if cannot flash, but tried to
            {
                //show failed attempt to use
                ui.HudButtonPressFail(ui.abilityFlashBox);
            }
        }
    }

    //turn player controls on or off
    public void ToggleInputOn(bool inputOn)
    {
        if(inputOn)
        {
            playerLocomotionInput.CarControls.Enable();
        }
        else
        {
            playerLocomotionInput.CarControls.Disable();
        }
    }

    //Auto movement -----------------------------------------------------------------------------------------------------------
    private void HandleAutoMovement()
    {
        if(autoDestinationQueue.Count >= 1)//if there is a destination
        {
            
            Vector3 moveDirection = (new Vector3((autoDestinationQueue[0].x - transform.position.x), 0, (autoDestinationQueue[0].z - transform.position.z))).normalized;
            moveDirection = (autoDestinationQueue[0] - transform.position);
            moveDirection = new Vector3(moveDirection.x, 0 ,moveDirection.z).normalized;

            Vector3 moveDelta = moveDirection * runAcceleration * Time.deltaTime;
            Vector3 newVel = characterController.velocity + moveDelta;
            newVel.y = 0;//no vertical speed limit

            //apply drag
            Vector3 curDrag = newVel.normalized * drag * Time.deltaTime;
            newVel = (newVel.magnitude > drag * Time.deltaTime) ? newVel - curDrag : Vector3.zero;

            //apply movement
            movementToApply += newVel;
            movementToApply.y += verticalVelocity;//and gravity and jump
            //move player
            characterController.Move(movementToApply * Time.deltaTime);

            autoDestTimeout += Time.deltaTime;
            if(autoDestTimeout > 3)//set 3 second limit to avoid impossible movement
            {
                autoDestinationQueue.RemoveAt(0);
                autoDestTimeout = 0;
                movementToApply = new Vector3(0, 0, 0);//zero movement
                if(autoDestinationQueue.Count == 0)
                {
                    isAutomoving = false;
                    AllowMovement(false);
                }
            }
            else
            {
                float dist = Vector3.Distance(new Vector3(autoDestinationQueue[0].x, 0, autoDestinationQueue[0].z), new Vector3(transform.position.x, 0, transform.position.z));
                if(dist < 0.05f)//if close enough to destination
                {
                    autoDestinationQueue.RemoveAt(0);
                    autoDestTimeout = 0;
                    movementToApply = new Vector3(0, 0, 0);//zero movement
                    if(autoDestinationQueue.Count == 0)
                    {
                        isAutomoving = false;
                        AllowMovement(false);
                    }
                }
            }
        }
    }

    public void AllowMovement(bool newState)
    {
        canMove = newState;
    }

    public void CancelAutoMovement()
    {
        autoDestinationQueue.Clear();
        isAutomoving = false;
    }

    public void SetCameraYAngleManual(float ang)
    {
        mainVCam.m_XAxis.Value = ang;
    }
}
