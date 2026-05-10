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

    private void Awake()
    {
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
        _server.Start();

        agent.SetExternalControl(true);
        Debug.Log($"[RacingGrpcServer] gRPC server listening on port {port}");
    }

    private void OnDestroy()
    {
        _server?.ShutdownAsync().Wait();
    }

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
            var result = UnityMainThreadDispatcher.Instance().EnqueueAndWait(() =>
            {
                UnityEngine.Random.InitState(request.Seed);

                _agent.EndEpisode();
                _agent.OnEpisodeBegin();

                return new ResetResponse
                {
                    Observation = { _agent.GetObservationVector() }
                };
            });

            return Task.FromResult(result);
        }

        public override Task<StepResponse> Step(
            StepRequest request,
            ServerCallContext context)
        {
            float throttle = request.Action[0];
            float steering = request.Action[1];

            var result = UnityMainThreadDispatcher.Instance().EnqueueAndWait(() =>
            {
                _agent.ApplyAction(throttle, steering);

                var obs = _agent.GetObservationVector();

                return new StepResponse
                {
                    Observation = { obs }
                };
            });

            return Task.FromResult(result);
        }
    }
}
