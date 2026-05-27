using UnityEngine;
using System.Collections;
using System;

public class PlayerController : MonoBehaviour {

    [SerializeField] float      m_speed = 4.0f;
    [SerializeField] float      m_fallSpeed = 5f;
    [SerializeField] float      m_jumpForce = 7.5f;
    [SerializeField] Vector2    m_wallJumpForce = new(5.5f, 10.5f);
    [SerializeField] float      m_wallJumpDelay = 0.2f;
    [SerializeField] float      m_wallJumpDragTime = 1.0f;
    [SerializeField] float      m_wallJumpDecay = 10.0f;
    [SerializeField] float      m_rollForce = 6.0f;
    [SerializeField] bool       m_noBlood = false;
    [SerializeField] GameObject m_slideDust;
    [SerializeField] GameObject m_cameraLookAheadPivot;

    private Animator            m_animator;
    private Rigidbody2D         m_body2d;
    private Sensor_HeroKnight   m_groundSensor;
    private Sensor_HeroKnight   m_wallSensorR1;
    private Sensor_HeroKnight   m_wallSensorR2;
    private Sensor_HeroKnight   m_wallSensorL1;
    private Sensor_HeroKnight   m_wallSensorL2;
    private bool                m_isWallSliding = false;
    private bool                m_isWallJumping = false;
    private bool                m_grounded = false;
    private bool                m_rolling = false;
    protected int               m_facingDirection = 1;
    private int                 m_currentAttack = 0;
    private float               m_timeSinceAttack = 0.0f;
    private float               m_delayToIdle = 0.0f;
    private float               m_rollDuration = 8.0f / 14.0f;
    private float               m_rollCurrentTime;


    // Use this for initialization
    void Start ()
    {
        m_animator = GetComponent<Animator>();
        m_body2d = GetComponent<Rigidbody2D>();
        m_groundSensor = transform.Find("GroundSensor").GetComponent<Sensor_HeroKnight>();
        m_wallSensorR1 = transform.Find("WallSensor_R1").GetComponent<Sensor_HeroKnight>();
        m_wallSensorR2 = transform.Find("WallSensor_R2").GetComponent<Sensor_HeroKnight>();
        m_wallSensorL1 = transform.Find("WallSensor_L1").GetComponent<Sensor_HeroKnight>();
        m_wallSensorL2 = transform.Find("WallSensor_L2").GetComponent<Sensor_HeroKnight>();
    }

    // Update is called once per frame
    void Update ()
    {
        // Increase timer that controls attack combo
        m_timeSinceAttack += Time.deltaTime;

        // Increase timer that checks roll duration
        if(m_rolling)
            m_rollCurrentTime += Time.deltaTime;

        // Disable rolling if timer extends duration
        if(m_rollCurrentTime > m_rollDuration)
            m_rolling = false;

        //Check if character just landed on the ground
        if (!m_grounded && m_groundSensor.State())
        {
            m_grounded = true;
            m_animator.SetBool("Grounded", m_grounded);
        }

        //Check if character just started falling
        if (m_grounded && !m_groundSensor.State())
        {
            m_grounded = false;
            m_animator.SetBool("Grounded", m_grounded);
        }

        // -- Handle input and movement --
        float inputX = Input.GetAxis("Horizontal");
        float inputXRaw = Input.GetAxisRaw("Horizontal");

        // Swap direction of sprite depending on walk direction
        float inputTolerance = m_isWallSliding ? 0.9f : 0;
        float inputSource = m_isWallSliding ? inputX : inputXRaw;

        if (inputSource > inputTolerance && !m_isWallJumping)
        {
            GetComponent<SpriteRenderer>().flipX = false;
            m_cameraLookAheadPivot.transform.localScale = new Vector3(1f, m_cameraLookAheadPivot.transform.localScale.y, 1f);
            m_facingDirection = 1;

        }
            
        else if (inputSource < inputTolerance * -1 && !m_isWallJumping)
        {
            GetComponent<SpriteRenderer>().flipX = true;
            m_cameraLookAheadPivot.transform.localScale = new Vector3(-1f, m_cameraLookAheadPivot.transform.localScale.y, 1f);
            m_facingDirection = -1;
        }

        // Move
        if (!m_rolling && !m_isWallJumping)
        {
            if (m_grounded)
            {
                m_body2d.linearVelocity = new Vector2(inputX * m_speed, m_body2d.linearVelocity.y);
            } else if(inputXRaw != 0)
            {
                m_body2d.linearVelocity = new Vector2(inputX * m_speed, m_body2d.linearVelocity.y);
            }
        }

        // Gravity
        if (m_body2d.linearVelocity.y < 0 && !m_grounded && !m_isWallSliding)
        {
            m_body2d.linearVelocity = new Vector2(m_body2d.linearVelocity.x, Math.Min(m_fallSpeed * -1, m_body2d.linearVelocity.x * 1.1f));
            // vertical flip camera look ahead, look down
            m_cameraLookAheadPivot.transform.localScale = new Vector3(m_cameraLookAheadPivot.transform.localScale.x, -1f, 1f);
        } else
        {
            // vertical flip camera look ahead, look normal
            m_cameraLookAheadPivot.transform.localScale = new Vector3(m_cameraLookAheadPivot.transform.localScale.x, 1f, 1f);
        }

        bool wasWallSliding = m_isWallSliding;

        // Wall Slide
        m_isWallSliding = (m_wallSensorR1.State() || m_wallSensorR2.State()) || (m_wallSensorL1.State() || m_wallSensorL2.State());
        m_animator.SetBool("WallSlide", m_isWallSliding);

        // Sticky wall slide
        if (m_isWallSliding)
        {
            if (m_body2d.linearVelocity.y > 0) 
                m_body2d.linearVelocity = new Vector2(m_body2d.linearVelocity.x, m_body2d.linearVelocity.y * 0.5f);
            
            if (! wasWallSliding)
            {
                m_body2d.linearVelocity = new Vector2(0f, 0f);
            }
        }

        //Set AirSpeed in animator
        m_animator.SetFloat("AirSpeedY", m_body2d.linearVelocity.y);

        // -- Handle Animations --

        //Death
        if (Input.GetKeyDown("e") && !m_rolling)
        {
            m_animator.SetBool("noBlood", m_noBlood);
            m_animator.SetTrigger("Death");
        }
            
        //Hurt
        else if (Input.GetKeyDown("q") && !m_rolling)
            m_animator.SetTrigger("Hurt");

        //Attack
        else if(Input.GetMouseButtonDown(0) && m_timeSinceAttack > 0.25f && !m_rolling)
        {
            m_currentAttack++;

            // Loop back to one after third attack
            if (m_currentAttack > 3)
                m_currentAttack = 1;

            // Reset Attack combo if time since last attack is too large
            if (m_timeSinceAttack > 1.0f)
                m_currentAttack = 1;

            // Call one of three attack animations "Attack1", "Attack2", "Attack3"
            m_animator.SetTrigger("Attack" + m_currentAttack);

            // Reset timer
            m_timeSinceAttack = 0.0f;
        }

        // Block
        else if (Input.GetMouseButtonDown(1) && !m_rolling)
        {
            m_animator.SetTrigger("Block");
            m_animator.SetBool("IdleBlock", true);
        }

        else if (Input.GetMouseButtonUp(1))
            m_animator.SetBool("IdleBlock", false);

        // Roll
        else if (Input.GetKeyDown("left shift") && !m_rolling && !m_isWallSliding)
        {
            m_rolling = true;
            m_animator.SetTrigger("Roll");
            m_body2d.linearVelocity = new Vector2(m_facingDirection * m_rollForce, m_body2d.linearVelocity.y);
        }
            

        //Jump
        else if (Input.GetKeyDown("space") && (m_grounded || m_isWallSliding) && !m_rolling)
        {
            // Wall Jumping
            if (m_isWallSliding && !m_grounded)
            {
                m_isWallJumping = true;
                m_isWallSliding = false;
                Invoke(nameof(WallJumpInputLock), m_wallJumpDelay);
                Invoke(nameof(WallJumpDrag), m_wallJumpDragTime);
                m_body2d.linearVelocity = new Vector2(m_facingDirection * -1 * m_wallJumpForce.x, m_wallJumpForce.y);
                m_wallSensorR1.Disable(m_wallJumpDelay);
                m_wallSensorR2.Disable(m_wallJumpDelay);
                m_wallSensorL1.Disable(m_wallJumpDelay);
                m_wallSensorL2.Disable(m_wallJumpDelay);

                GetComponent<SpriteRenderer>().flipX = ! GetComponent<SpriteRenderer>().flipX;
                m_facingDirection *= -1;

            } else // regular Jump
            {
                m_body2d.linearVelocity = new Vector2(m_body2d.linearVelocity.x, m_jumpForce);
                m_groundSensor.Disable(0.2f);
            }

            m_animator.SetTrigger("Jump");
            m_grounded = false;
            m_animator.SetBool("Grounded", m_grounded);
        }
        // pressure sensitive jump
        else if (Input.GetKeyUp("space") && m_body2d.linearVelocity.y > 0)
        {
            m_body2d.linearVelocity = new Vector2(m_body2d.linearVelocity.x, m_body2d.linearVelocity.y * 0.5f);
        }

        //Run
        else if (Mathf.Abs(inputX) > Mathf.Epsilon)
        {
            // Reset timer
            m_delayToIdle = 0.05f;
            m_animator.SetInteger("AnimState", 1);
        }

        //Idle
        else
        {
            // Prevents flickering transitions to idle
            m_delayToIdle -= Time.deltaTime;
                if(m_delayToIdle < 0)
                    m_animator.SetInteger("AnimState", 0);
        }
    }

    void WallJumpInputLock() => m_isWallJumping = false;

    void WallJumpDrag()
    {
        Mathf.Lerp(m_body2d.linearVelocity.x, 0f, m_wallJumpDecay * Time.deltaTime);
    }

    // Animation Events
    // Called in slide animation.
    void AE_SlideDust()
    {
        Vector3 spawnPosition;

        if (m_facingDirection == 1)
            spawnPosition = m_wallSensorR2.transform.position;
        else
            spawnPosition = m_wallSensorL2.transform.position;

        if (m_slideDust != null)
        {
            // Set correct arrow spawn position
            GameObject dust = Instantiate(m_slideDust, spawnPosition, gameObject.transform.localRotation) as GameObject;
            // Turn arrow in correct direction
            dust.transform.localScale = new Vector3(m_facingDirection, 1, 1);
        }
    }

    public int GetFacingDir()
    {
        return m_facingDirection;
    }

    public void SetFacingDir(int facingDir)
    {
        m_facingDirection = facingDir;
    }
}
