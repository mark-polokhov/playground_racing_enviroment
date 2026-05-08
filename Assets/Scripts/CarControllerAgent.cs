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

    private bool ckptChanged;
    private bool wrongCkpt;

    private int stepCount;
    private int maxSteps = 300000;

    private Vector3 startPosition;
    private Quaternion startRotation;

    private bool isTouchingWall;

    private bool done;

    void Awake()
    {
        if (!carController)
            carController = GetComponent<CarController>();

        if (!rb)
            rb = GetComponent<Rigidbody>();

        if (!splineStats)
            splineStats = GetComponent<CarSplineStats>();

        if (!spline)
            spline = FindObjectOfType<SplineCalculator>();

        startPosition = transform.position;
        startRotation = transform.rotation;
    }

    void Start()
    {
        trackCheckpoints.OnPlayerCorrectCheckpoint += OnCorrect;
        trackCheckpoints.OnPlayerWrongCheckpoint += OnWrong;
    }

    private void OnCorrect(object sender, TrackCheckpoints.CarCheckpointEventArgs e)
    {
        if (e.carTransform == transform)
            ckptChanged = true;
    }

    private void OnWrong(object sender, TrackCheckpoints.CarCheckpointEventArgs e)
    {
        if (e.carTransform == transform)
            wrongCkpt = true;
    }

    // =====================================================
    // OBSERVATIONS
    // =====================================================

    public float[] GetObservationVector()
    {
        float[] obs = new float[18];

        float latDist = splineStats.GetDistanceToSpline();
        obs[0] = Mathf.Clamp(latDist / maxTrackWidth, -1f, 1f);

        obs[1] = splineStats.GetProgressAlongSpline();

        float cur = splineStats.GetLocalCurvature();

        obs[2] = cur;
        obs[3] = cur;
        obs[4] = cur;
        obs[5] = 0f;
        obs[6] = 0f;

        float distCkpt = trackCheckpoints.GetDistanceToNextCheckpoint(transform);
        obs[7] = Mathf.Clamp(distCkpt / maxCheckpointDist, 0f, 1f);

        var ckpt = trackCheckpoints.GetNextCheckpoint(transform);

        if (ckpt != null)
        {
            Vector3 dir =
                (ckpt.transform.position - transform.position).normalized;

            obs[8] = Vector3.Dot(transform.forward, dir);
        }
        else
        {
            obs[8] = 0f;
        }

        Vector3 localVel =
            transform.InverseTransformDirection(rb.velocity);

        float forwardSpeed = localVel.z;
        float lateralSpeed = localVel.x;

        obs[9] = Mathf.Clamp(forwardSpeed / maxSpeed, -1f, 1f);
        obs[10] = Mathf.Clamp(lateralSpeed / maxSpeed, -1f, 1f);

        float speed = rb.velocity.magnitude;
        obs[11] = Mathf.Clamp(speed / maxSpeed, 0f, 1f);

        // obs[12] = Mathf.Clamp(localVel.z / maxSpeed, -1f, 1f);
        // obs[13] = Mathf.Clamp(localVel.x / maxSpeed, -1f, 1f);
    
        obs[13] = isTouchingWall ? 1f : 0f;

        obs[14] = 0.5f;
        obs[15] = 0.5f;

        obs[16] = ckptChanged ? 1f : 0f;
        obs[17] = wrongCkpt ? 1f : 0f;


        ckptChanged = false;
        wrongCkpt = false;

        return obs;
    }

    // =====================================================
    // ACTION
    // =====================================================

    public void ApplyAction(float throttle, float steering)
    {
        throttle = Mathf.Clamp(throttle, -1f, 1f);
        steering = Mathf.Clamp(steering, -1f, 1f);

        carController.SetInput(throttle, steering);
    }

    // =====================================================
    // MANUAL PHYSICS STEP
    // =====================================================

    public void SimulateStep()
    {
        carController.StepPhysics();

        Physics.Simulate(Time.fixedDeltaTime);

        stepCount++;

        if (stepCount >= maxSteps)
            done = true;
    }

    // =====================================================

    public bool IsDone()
    {
        return done;
    }

    public void ResetAgent()
    {
        ResetAgent(startPosition, startRotation);
    }

    public void ResetAgent(Vector3 pos, Quaternion rot)
    {
        transform.position = pos;
        transform.rotation = rot;

        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        trackCheckpoints.ResetCheckpoint(transform);

        stepCount = 0;
        done = false;

        ckptChanged = false;
        wrongCkpt = false;
    }

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

    // ===== LEGACY =====

    public float GetCumulativeReward() => 0f;

    public void SetExternalControl(bool v) { }

    public bool IsPlayer() => false;

    public float ProgressAgent()
        => splineStats.GetProgressAlongSpline();

    public int ChecksOver() => 0;
}