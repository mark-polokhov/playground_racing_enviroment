using System.Threading.Tasks;
using Grpc.Core;
using UnityEngine;
using UnityRacing;

/// <summary>
/// gRPC-сервер для RL-клиента (Python). Реализует UnityRacingService.
/// Настройка: добавь на GameObject, укажи CarControllerAgent. Порт 50051.
/// Требуется: Grpc.Core и Google.Protobuf (через NuGet For Unity).
/// </summary>
[AddComponentMenu("Racing/Unity Racing gRPC Server")]
public class UnityRacingGrpcServer : MonoBehaviour
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
            Debug.LogError("[UnityRacingGrpcServer] CarControllerAgent not found!");
            return;
        }

        UnityMainThreadDispatcher.Instance();

        var impl = new RacingServiceImpl(agent, this);
        _server = new Server
        {
            Services = { UnityRacing.UnityRacingService.BindService(impl) },
            Ports = { new ServerPort("0.0.0.0", port, ServerCredentials.Insecure) }
        };
        _server.Start();

        _lastCumulativeReward = agent.GetCumulativeReward();
        agent.SetExternalControl(true);
        Debug.Log($"[UnityRacingGrpcServer] gRPC server listening on port {port}");
    }

    private void OnDestroy()
    {
        _server?.ShutdownAsync().Wait();
    }

    internal void SetLastCumulativeReward(float value) => _lastCumulativeReward = value;
    internal float GetLastCumulativeReward() => _lastCumulativeReward;

    private sealed class RacingServiceImpl : UnityRacing.UnityRacingService.UnityRacingServiceBase
    {
        private readonly CarControllerAgent _agent;
        private readonly UnityRacingGrpcServer _owner;

        public RacingServiceImpl(CarControllerAgent agent, UnityRacingGrpcServer owner)
        {
            _agent = agent;
            _owner = owner;
        }

        public override Task<ResetResponse> Reset(ResetRequest request, ServerCallContext context)
        {
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
                    Done = _agent.IsDone(),
                    Observation = { obs }
                };
            });

            return Task.FromResult(result);
        }
    }
}
