using System.Threading.Tasks;
using Grpc.Core;
using UnityEngine;
using Racing;

/// <summary>
/// gRPC-сервер для RL-клиента (Python). Реализует RacingService.
/// Настройка: добавь на GameObject, укажи CarControllerAgent. Порт 50051.
/// Требуется: Grpc.Core и Google.Protobuf (через NuGet For Unity).
/// </summary>
[AddComponentMenu("Racing/Unity Racing gRPC Server")]
public class RacingGrpcServer : MonoBehaviour
{
    [SerializeField] private CarControllerAgent agent;
    [SerializeField] private int port = 50051;

    private Server _server;
    private float _lastCumulativeReward;
    private const int FixedFramesPerStep = 1;

    private void Awake()
    {
        Physics.autoSimulation = false;
        Time.fixedDeltaTime = 0.02f;
        if (agent == null)
            agent = FindObjectOfType<CarControllerAgent>();
    }

    private void Start()
    {
        if (agent == null)
        {
            Debug.LogError("[RacingGrpcServer] CarControllerAgent not found!");
            return;
        }

        UnityMainThreadDispatcher.Instance();

        var impl = new RacingServiceImpl(agent, this);
        _server = new Server
        {
            Services = { Racing.RacingService.BindService(impl) },
            Ports = { new ServerPort("0.0.0.0", port, ServerCredentials.Insecure) }
        };
        
        try
        {
            Debug.Log("[gRPC] Starting server...");

            _server.Start();

            Debug.Log($"[gRPC] Server started on port {port}");
        }
        catch (System.Exception e)
        {
            Debug.LogError("[gRPC] SERVER START FAILED");
            Debug.LogError(e);
        }

        _lastCumulativeReward = agent.GetCumulativeReward();
        agent.SetExternalControl(true);
        Debug.Log($"[RacingGrpcServer] gRPC server listening on port {port}");
    }

    private void OnDestroy()
    {
        _server?.ShutdownAsync().Wait();
    }

    internal void SetLastCumulativeReward(float value) => _lastCumulativeReward = value;
    internal float GetLastCumulativeReward() => _lastCumulativeReward;

    private sealed class RacingServiceImpl : Racing.RacingService.RacingServiceBase
    {
        private readonly CarControllerAgent _agent;
        private readonly RacingGrpcServer _owner;

        public RacingServiceImpl(CarControllerAgent agent, RacingGrpcServer owner)
        {
            _agent = agent;
            _owner = owner;
        }

        public override Task<ResetResponse> Reset(ResetRequest request, ServerCallContext context)
        {
            Debug.Log("[GRPC] Reset called");
            var result = UnityMainThreadDispatcher.Instance().EnqueueAndWait(() =>
            {
                Random.InitState(request.Seed);

                _agent.ResetAgent();

                for (int i = 0; i < 5; i++)
                    Physics.Simulate(Time.fixedDeltaTime);

                return new ResetResponse
                {
                    Observation = { _agent.GetObservationVector() }
                };
            });

            return Task.FromResult(result);
        }

        public override Task<StepResponse> Step(StepRequest request, ServerCallContext context)
        {
            float throttle = request.Action[0];
            float steering = request.Action[1];

            var result = UnityMainThreadDispatcher.Instance().EnqueueAndWait(() =>
            {
                _agent.ApplyAction(throttle, steering);

                // FRAME SKIP
                for (int i = 0; i < 5; i++)
                {
                    Physics.Simulate(Time.fixedDeltaTime);
                }

                var obs = _agent.GetObservationVector();

                return new StepResponse
                {
                    // Done = _agent.IsDone(), // ??? KOSTYL
                    Observation = { obs }
                };
            });

            return Task.FromResult(result);
        }
    }
}
