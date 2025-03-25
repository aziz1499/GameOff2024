using System.Collections;
using System.Collections.Generic;// ---> Ajout — pour List<>
using Cinemachine;
using UnityEngine;
using UnityEngine.AI;

public enum DisguiseGroups
{
    None = 1,
    Red = 2,
    Blue = 3,
    Green = 5,
    Yellow = 7
}

public class PatrolNavigation : MonoBehaviour
{
    enum EnemyState
    {
        NullState,      //0 - null(idle)
        Waiting,        //1 - transition between states
        Patrolling,     //2 - basic movement along route
        Navigating,     //3 - move to certain location
        Finding,        //4 - looking around for the player
        Spotting,       //5 - currently seeing the player
        Stunned        //6 - unable to move or detect the player
    }

    public NavMeshAgent agent;
    [SerializeField] private Animator enemyAnim;
    private EnemyState currentState = EnemyState.Patrolling;
    private GameObject player;
    private Camera playerCamera;
    private float startRot;
    private float startTime;
    private PlayerController playerController; // -----> Ajout d’un lien vers le script du joueur(nouvel algorithme)
    //Permet d’accéder à la variable currentDisguise du joueur.

    [Header("Suspicion")]
    public float suspicionMeter = 0;
    public float suspicionRate = 20;
    public GameObject suspicionIcon;
    [SerializeField] private SpriteRenderer suspicionIconRenderer;
    [SerializeField] private GameObject suspicionMask;
    [HideInInspector] public static PatrolNavigation gameOverEnemy;//a variable shared between PatrolNavigation instances to identify which enemy ended the game
    [SerializeField] private CinemachineVirtualCamera personalVCam;
    public GameObject suspicionIconMinimap;

    //J ai remplace le tableau Transform[] utilise pour stocker les points de patrouille
    //par une List<Transform>, ce qui me permet deavoir une structure de donnees plus flexible.
    //Contrairement aux tableaux, les listes peuvent etre modifiees dynamiquement,
    //ce qui est utile si je souhaite faire evoluer le comportement de l ennemi ou gerer des evenements dans le jeu.
    [Header("Patrol Route")]
    [SerializeField] private List<Transform> patrolPath = new List<Transform>(); // ----->Ajout — remplace Transform[]
    [SerializeField] private int patrolPointer = 0;
    private float defaultSpeed;

    [Header("Stun")]
    [SerializeField] private EnemyConeDetection torchLight;
    public GameObject stunIcon;
    [SerializeField] private float stunDuration = 3;
    private bool isStunned;
    private float stunTimer;
    [SerializeField] private GameObject stunMask;
    public GameObject stunIconMinimap;

    [Header("Disguise")]//success of diguise calculated using prime numbers
    public DisguiseGroups disguiseNeeded = DisguiseGroups.None;//id of disguise needed by player to avoid detection by this when still.  1 - none, 2 - red, 3 - blue, 5 - green, 7 - yellow

    [Header("Sound Effects")]
    [SerializeField] private AudioSource suspicionSound;
    [SerializeField] private AudioSource alertSound;

    // Start is called before the first frame update
    void Start()
    {
        playerCamera = Camera.main;
        player = FindObjectOfType<PlayerController>().gameObject;
        defaultSpeed = agent.speed;
        playerController = player.GetComponent<PlayerController>(); // -----> initialisation Permet d'accéder aux infos du joueur dès le début.(nouvel algorithme)

        //de-parent all "PatrolWaypoint" children (parented for organisation of patrols)
        if (agent.transform.Find("PatrolWaypointGroup") != null)
        {
            agent.transform.Find("PatrolWaypointGroup").parent = null;
        }
        //set patrol start path
        if (patrolPath.Count > 0)
        {
            if ((patrolPointer >= patrolPath.Count) || (patrolPointer < 0))
            //-------> modifié : .Length par .Count
            //-------->j’ai également dû remplacer la propriété .Length par .Count,
            //------->car les listes utilisent .Count pour connaître le nombre d’éléments.  
            {
                patrolPointer = 0;
            }
            agent.nextPosition = patrolPath[patrolPointer].position;//teleport to first waypoint
            if (currentState == EnemyState.Patrolling)
            {
                enemyAnim.SetBool("isWalking", true);
                MoveToNextWaypoint();//start patrolling
            }
        }
    }

    // Update is called once per frame
    void Update()
    {
        //act based on current state
        switch (currentState)
        {
            case EnemyState.Waiting:
                DoWaiting();
                break;
            case EnemyState.Patrolling:
                DoPatroling();
                break;
            case EnemyState.Navigating:
                DoNavigating();
                break;
            case EnemyState.Finding:
                DoFinding();
                break;
            case EnemyState.Spotting:
                DoSpotting();
                break;
            case EnemyState.Stunned:
                DoStunned();
                break;
            default:
                break;
        }
        UpdateSuspicion(); // ------>Appelle notre algorithme à chaque frame du jeu
    }

    void FixedUpdate()
    {
        //have icons always face player
        suspicionIcon.transform.LookAt(playerCamera.transform);
        stunIcon.transform.LookAt(playerCamera.transform);
        //set mask heights (and color)
        suspicionMask.transform.localPosition = new Vector3(0, (Mathf.Min(suspicionMeter, 100) * 0.09f) + 0.5f, 0);
        suspicionIconRenderer.color = Color.HSVToRGB((60 - (Mathf.Min(suspicionMeter, 100) * 0.6f)) / 360f, 0.95f, 0.95f);
        stunMask.transform.localPosition = new Vector3(0, (Mathf.Min(stunTimer, stunDuration) * (9f / stunDuration)) + 0.5f, 0);
        //stun time
        if (isStunned)
        {
            stunTimer -= Time.fixedDeltaTime;
            if (stunTimer < 0)
            {
                stunTimer = 0;
            }
        }
    }

    public void NullifyState()//Set state to null, effectively disabling this
    {
        CancelInvoke();
        if (enemyAnim != null)
        {
            enemyAnim.SetBool("isWalking", false);
        }
        currentState = EnemyState.NullState;
    }

    //State Functions++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
    private void DoWaiting()//Waiting State----------------------------------------------------------------------------------------------------
    {
        agent.ResetPath();
        CancelInvoke();
        agent.speed = defaultSpeed;
        Invoke("MoveToNextWaypoint", 0.5f);//return to patrol
        enemyAnim.SetBool("isWalking", false);
        currentState = EnemyState.NullState;
    }

    private void MoveToNextWaypoint()
    {
        torchLight.gameObject.SetActive(true);
        currentState = EnemyState.Patrolling;
        agent.SetDestination(patrolPath[patrolPointer].position);//go to next place
        enemyAnim.SetBool("isWalking", true);
        enemyAnim.SetBool("isDizzy", false);
        patrolPointer = (patrolPointer + 1) % patrolPath.Count;//----> modifie : .Length → .Count 
        stunIcon.SetActive(false);
        stunIconMinimap.SetActive(false);
        isStunned = false;
    }

    private void DoPatroling()//Patrolling State----------------------------------------------------------------------------------------------------
    {
        if (((!agent.pathPending) && (agent.remainingDistance < 0.5f)) || (agent.pathStatus == NavMeshPathStatus.PathInvalid))//if patrol destination reached or failed
        {
            currentState = EnemyState.Waiting;
        }
    }

    private void DoNavigating()//Navigating State----------------------------------------------------------------------------------------------------
    {
        if (((!agent.pathPending) && (agent.remainingDistance < 0.5f)) || (agent.pathStatus == NavMeshPathStatus.PathInvalid))//if navigation destination reached or failed
        {
            currentState = EnemyState.Finding;
            startRot = agent.transform.eulerAngles.y;//initial rotation for looking around
            startTime = Time.timeSinceLevelLoad;
        }
    }

    public void NavigationAlert(Vector3 searchLocation)//Tell enemy where to go to
    {
        NavigationStart(searchLocation);
        suspicionMeter = Mathf.Max(20, suspicionMeter);//set suspicion when alerted
        suspicionSound.Play();
        CheckForEndGame();
    }

    public void NavigationStart(Vector3 searchLocation)//Tell enemy where to go to
    {
        CancelInvoke();
        agent.speed = defaultSpeed;
        currentState = EnemyState.Navigating;
        agent.SetDestination(searchLocation);//go to navigate location
        enemyAnim.SetBool("isWalking", true);
        suspicionIcon.SetActive(true);
        suspicionIconMinimap.SetActive(true);
    }

    public void DelayedNavigation(Vector3 searchLocation)
    {
        agent.speed = 0;
        currentState = EnemyState.Navigating;
        agent.SetDestination(searchLocation);//go to navigate location
        enemyAnim.SetBool("isWalking", false);
        CancelInvoke();
        Invoke("EndDelay", 2);//start navigating
    }

    private void EndDelay()
    {
        agent.speed = defaultSpeed;
        enemyAnim.SetBool("isWalking", true);
    }

    private void DoFinding()//Finding State----------------------------------------------------------------------------------------------------
    {
        agent.ResetPath();
        enemyAnim.SetBool("isWalking", false);
        CancelInvoke();
        agent.speed = defaultSpeed;
        suspicionMeter = suspicionMeter - (Time.deltaTime * suspicionRate);//decrease suspicion while searching
        //rotate enemy to search
        agent.transform.eulerAngles = new Vector3(agent.transform.eulerAngles.x, startRot + (60 * Mathf.Sin(Time.timeSinceLevelLoad - startTime)), agent.transform.eulerAngles.z);
        //if suspicion gone, continue patrol
        if (suspicionMeter <= 0)
        {
            suspicionMeter = 0;
            suspicionIcon.SetActive(false);
            suspicionIconMinimap.SetActive(false);
            suspicionSound.Stop();
            Invoke("MoveToNextWaypoint", 0.5f);//return to patrol after 1 second
            currentState = EnemyState.NullState;
        }
    }

    private void DoSpotting()//Spotting State----------------------------------------------------------------------------------------------------
    {
        TurnTowardsPlayer();
        //if too far, move closer
        Vector3 endPos = new Vector3(player.transform.position.x, agent.transform.position.y, player.transform.position.z);
        if (Vector3.Distance(agent.transform.position, endPos) > 10)
        {
            agent.SetDestination(endPos);
            enemyAnim.SetBool("isWalking", true);
        }
        else
        {
            agent.ResetPath();
            enemyAnim.SetBool("isWalking", false);
        }
        //increment suspicion meter
        suspicionMeter += (Time.deltaTime * suspicionRate);
        suspicionIcon.SetActive(true);
        suspicionIconMinimap.SetActive(true);
        CheckForEndGame();
    }

    public void SpotPlayer()
    {
        agent.ResetPath();
        enemyAnim.SetBool("isWalking", false);
        CancelInvoke();
        agent.speed = defaultSpeed;
        currentState = EnemyState.Spotting;
        suspicionSound.Play();
    }

    private void TurnTowardsPlayer()
    {
        TurnTowardsPlayer(0.75f);
    }

    private void TurnTowardsPlayer(float turnSpeed)
    {
        //turn to face player
        Vector3 startPos = agent.transform.position;
        Vector3 endPos = new Vector3(player.transform.position.x, agent.transform.position.y, player.transform.position.z);
        Vector3 direVect = (endPos - startPos).normalized;
        float speedMultiplier = Vector3.Distance(startPos, endPos) < 5 ? 4 : 2;//increase turn speed if player is very close
        if (Mathf.Abs(Vector3.SignedAngle(agent.transform.forward, direVect, Vector3.up)) > 7.5f)
        {
            if (Vector3.SignedAngle(agent.transform.forward, direVect, Vector3.up) > 0)
            {
                agent.transform.Rotate(new Vector3(0, turnSpeed * speedMultiplier, 0));
            }
            else
            {
                agent.transform.Rotate(new Vector3(0, -turnSpeed * speedMultiplier, 0));
            }
        }
    }

    private void DoStunned()//Stunned State----------------------------------------------------------------------------------------------------
    {
        agent.ResetPath();
        enemyAnim.SetBool("isWalking", false);
        enemyAnim.SetBool("isDizzy", true);
        CancelInvoke();
        agent.speed = defaultSpeed;
        torchLight.TurnOffTorch();
        torchLight.gameObject.SetActive(false);//turn off torch
        Invoke("MoveToNextWaypoint", stunDuration);//return to patrol
        //start stun icon animation
        isStunned = true;
        stunTimer = stunDuration;
        currentState = EnemyState.NullState;
    }

    public void BecomeStunned()
    {
        agent.ResetPath();
        enemyAnim.SetBool("isWalking", false);
        enemyAnim.SetBool("isDizzy", true);
        suspicionMeter = 0;
        suspicionSound.Stop();
        suspicionIcon.SetActive(false);
        suspicionIconMinimap.SetActive(false);
        stunIcon.SetActive(true);
        stunIconMinimap.SetActive(true);
        CancelInvoke();
        agent.speed = defaultSpeed;
        currentState = EnemyState.Stunned;
    }

    //++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++

    private void CheckForEndGame()
    {
        if (suspicionMeter >= 100)
        {
            suspicionMeter = 100;
            suspicionSound.Stop();
            if (gameOverEnemy == null)//stop multiple enemies from running this script at the same time
            {
                gameOverEnemy = this;
                enemyAnim.SetBool("isWalking", false);
                enemyAnim.SetBool("isDizzy", false);
                currentState = EnemyState.NullState;
                CancelInvoke();
                agent.ResetPath();
                player.GetComponent<PlayerController>().ToggleInputOn(false);//disable input
                FindObjectOfType<CinemachineInputProvider>().enabled = false;//disable camera controls
                FindObjectOfType<UIScripts>().HUDOut();//turn off hud
                alertSound.Play();
                //pan camera to player
                if (personalVCam != null)
                {
                    personalVCam.enabled = true;
                    EndGamePostCameraPan();
                }
                else
                {
                    EndGamePostCameraPan();//even if no pan, run function anyway
                }
            }
        }
    }

    public void EndGamePostCameraPan()//the engame functionality after the camera pan
    {
        //Lose Game
        FindObjectOfType<UIScripts>().ShowLevelLoseScreen();
    }
    void UpdateSuspicion() 
        // ----->Ajout nouvel algorithme adapte la vitesse d’augmentation de la suspicion en fonction du déguisement.
    {
        if (playerController == null) return;

        if (playerController.currentDisguise == disguiseNeeded)
        {
            suspicionMeter += Time.deltaTime * (suspicionRate * 0.2f);
        }
        else
        {
            suspicionMeter += Time.deltaTime * suspicionRate;
        }

        suspicionMeter = Mathf.Clamp(suspicionMeter, 0, 100);
    }

}
