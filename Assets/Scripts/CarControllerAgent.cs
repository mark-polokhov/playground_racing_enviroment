using UnityEngine;

public class CarControllerAgent : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private CarController carController;
    [SerializeField] private Rigidbody rb;
    [SerializeField] private TrackCheckpoints trackCheckpoints;
    [SerializeField] private CarSplineStats splineStats;
    [SerializeField] private SplineCalculator spline;

    [Header("Config")]
    [SerializeField] private float maxSpeed = 50f;
    [SerializeField] private float maxTrackWidth = 10f;
    [SerializeField] private float maxCheckpointDist = 50f;
    
    [SerializeField] private bool isPlayer = false;
    [SerializeField] private Transform spawnPosition;
    private bool ckptChanged;
    private bool wrongCkpt;

    private int stepCount;
    private int maxSteps = 300000;

    private Vector3 startPosition;
    private Quaternion startRotation;

    private int previousSplineIndex = 0;
    private int currentSplineIndex = 0;
    
    private bool start = false;
    private int checksOver = 0;

    private bool isTouchingWall;

    private bool done;

    private AIController aiController;

    private bool _episodeDone = false;
    private bool _externalControl = false;

    void Awake()
    {
        carController = GetComponent<CarController>();
        rb = GetComponent<Rigidbody>();
        splineStats = GetComponent<CarSplineStats>();
        spline = FindObjectOfType<SplineCalculator>();
        aiController = GetComponent<AIController>();

        startPosition = transform.position;
        startRotation = transform.rotation;
    }

    void Start()
    {
        checksOver = 0;
        start = true;
        trackCheckpoints.OnPlayerCorrectCheckpoint += OnCorrect;
        trackCheckpoints.OnPlayerWrongCheckpoint += OnWrong;
    }

    private void OnCorrect(object sender, TrackCheckpoints.CarCheckpointEventArgs e)
    {
        if (e.carTransform == transform)
        {
            ckptChanged = true;
            checksOver++;
        }

    }

    private void OnWrong(object sender, TrackCheckpoints.CarCheckpointEventArgs e)
    {
        if (e.carTransform == transform)
            wrongCkpt = true;
    }


    private void FixedUpdate()
    {
        if (!start || _externalControl) return;

        float forwardAmount, turnAmount;
        if (isPlayer)
        {
            forwardAmount = Input.GetAxis("Vertical");
            turnAmount = Input.GetAxis("Horizontal");
        }
        else if (aiController != null)
        {
            forwardAmount = aiController.InputVerticalAI();
            turnAmount = aiController.InputHorizontalAI();
        }
        else
        {
            return;
        }

        ApplyAction(forwardAmount, turnAmount);
    }

    void Update()
    {
        
    }

    public void OnEpisodeBegin()
    {
        transform.position = spawnPosition.position + new Vector3(Random.Range(-1f,1f),0,Random.Range(-1f,1f));
        transform.forward = spawnPosition.forward;
        trackCheckpoints.ResetCheckpoint(transform);
        checksOver = 0;

        stepCount = 0;
        _episodeDone = false;
        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
    }

    public float ProgressAgent()
    {
        return splineStats.GetProgressAlongSpline();
    }

    public void EndEpisode()
    {
        OnEpisodeBegin();
    }

    public bool IsEpisodeDone()
    {
        return _episodeDone || checksOver == TrackCheckpoints.GetChecks() || stepCount >= maxSteps;
    }

    // =====================================================
    // OBSERVATIONS
    // =====================================================

    public float[] GetObservationVector()
    {
        float[] obs = new float[12];

        float latDist = splineStats.GetDistanceToSpline();
        obs[0] = Mathf.Clamp(latDist / maxTrackWidth, -1f, 1f);
        // obs[1] = GetForwardProgressDelta();
        obs[1] = splineStats.GetProgressAlongSpline();
        obs[2] = splineStats.GetAngleToSplineDirection();
        obs[3] = splineStats.GetLocalCurvature();

        // float cur = splineStats.GetLocalCurvature();
        // obs[4] = cur;

        // obs[5] = splineStats.GetForwardDotSplineTangent();

        obs[4] = ckptChanged ? 1f : 0f;
        obs[5] = wrongCkpt ? 1f : 0f;

        float distCkpt = trackCheckpoints.GetDistanceToNextCheckpoint(transform);
        obs[6] = Mathf.Clamp(distCkpt / maxCheckpointDist, 0f, 1f);
        
        var ckpt = trackCheckpoints.GetNextCheckpoint(transform);

        if (ckpt != null)
        {
            Vector3 dir =
                (ckpt.transform.position - transform.position).normalized;

            obs[7] = Vector3.Dot(transform.forward, dir);
        }
        else
        {
            obs[7] = 0f;
        }

        float speed = rb.velocity.magnitude;
        obs[8] = Mathf.Clamp(speed / maxSpeed, 0f, 1f);

        Vector3 localVel =
            transform.InverseTransformDirection(rb.velocity);

        float forwardSpeed = localVel.z;
        float lateralSpeed = localVel.x;

        obs[9] = Mathf.Clamp(forwardSpeed / maxSpeed, -1f, 1f);
        obs[10] = Mathf.Clamp(lateralSpeed / maxSpeed, -1f, 1f);

        obs[11] = isTouchingWall ? 1f : 0f;

        ckptChanged = false;
        wrongCkpt = false;

        return obs;
    }

    // =====================================================
    // ACTION
    // =====================================================

    public void ApplyAction(float throttle, float steering)
    {
        carController.SetInput(throttle, steering);

        stepCount++;
        if (stepCount >= maxSteps)
        {
            if (_externalControl)
                _episodeDone = true;
            else
                EndEpisode();
        }
    }

    // =====================================================
    // MANUAL PHYSICS STEP
    // =====================================================

    // public void SimulateStep()
    // {
    //     carController.StepPhysics();

    //     Physics.Simulate(Time.fixedDeltaTime);

    //     stepCount++;

    //     if (stepCount >= maxSteps)
    //         done = true;
    // }

    // =====================================================

    public bool IsDone()
    {
        return done;
    }

    // public void ResetAgent()
    // {
    //     ResetAgent(startPosition, startRotation);
    // }

    // public void ResetAgent(Vector3 pos, Quaternion rot)
    // {
    //     transform.position = pos;
    //     transform.rotation = rot;

    //     rb.velocity = Vector3.zero;
    //     rb.angularVelocity = Vector3.zero;

    //     trackCheckpoints.ResetCheckpoint(transform);

    //     stepCount = 0;
    //     done = false;

    //     ckptChanged = false;
    //     wrongCkpt = false;
    // }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Wall"))
        {
            isTouchingWall = true;
        }
    }

    private void OnCollisionStay(Collision collision)
    {
        if (collision.gameObject.CompareTag("Wall"))
        {
            isTouchingWall = true;
        }
    }

    private void OnCollisionExit(Collision collision)
    {
        if (collision.gameObject.CompareTag("Wall"))
        {
            isTouchingWall = false;
        }
    }

    public float GetForwardProgressDelta()
    {
        int totalPoints = spline.splinePoints.Length;

        currentSplineIndex = splineStats.GetClosestSplineIndex();

        int delta = currentSplineIndex - previousSplineIndex;

        // Обработка перехода через финиш
        if (delta > totalPoints / 2)
            delta -= totalPoints;

        if (delta < -totalPoints / 2)
            delta += totalPoints;

        previousSplineIndex = currentSplineIndex;

        // Нормализуем
        return (float)delta / totalPoints;
    }

    // ===== LEGACY =====

    public float GetCumulativeReward() => 0f;

    public void SetExternalControl(bool value) => _externalControl = value;

    public bool IsPlayer() => isPlayer;

    public int ChecksOver() => checksOver;
}