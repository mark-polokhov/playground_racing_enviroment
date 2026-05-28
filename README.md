# RL-автогонка (gRPC)

Проект Unity (2022.3.51f1) с автогонками и внешним RL-обучением через gRPC. Агент управляется Python-клиентом, который получает наблюдения и отправляет действия по протоколу gRPC.

## Быстрый старт

### Требования
- Unity **2022.3.51f1**
- NuGetForUnity (подключён в `Packages/manifest.json`)

### Первый запуск после клонирования

1. Откройте проект в Unity **2022.3.51f1**.
2. Дождитесь завершения импорта (все DLL уже в `Assets/Plugins/gRPC/`).
3. Откройте сцену `Assets/Scenes/Env_Alone.unity`.
4. Нажмите **Play**.

### Запуск с gRPC (внешнее RL-обучение)

1. Запустите сцену в Unity (Play).
2. gRPC-сервер стартует на порту **50051** (`UnityRacingGrpcServer`).
3. Подключитесь Python-клиентом и используйте RPC `Reset` / `Step`.

## Архитектура

```
Python RL-клиент  ←— gRPC (порт 50051) —→  Unity (CarControllerAgent)
     │                                           │
     │  StepRequest(action[2])                   │  ApplyAction(fwd, turn)
     │  StepResponse(obs[9], reward, done)       │  GetObservationVector()
     │  ResetRequest(seed)                       │  OnEpisodeBegin()
     │  ResetResponse(obs[9])                    │
```

### Протокол (unity_racing.proto)

```protobuf
service UnityRacingService {
  rpc Reset(ResetRequest) returns (ResetResponse);
  rpc Step(StepRequest) returns (StepResponse);
}
```

- **Действия**: 2 continuous float — `[forward, turn]`, каждый в `[-1, 1]`
- **Наблюдения**: 9 float — расстояние до сплайна, прогресс, угол к сплайну, кривизна, расстояние до чекпоинта, dot направлений, скорость, длительность непрерывного контакта со стеной, флаг движения назад по трассе

## Управление

- **W/S** — газ/тормоз
- **A/D** — поворот
- **Esc** — пауза
- **E** — переключение камер

## Сцены

- `Assets/Scenes/Env_Alone.unity` — одиночная трасса
- `Assets/Scenes/Env_Duo.unity` — несколько агентов
- `Assets/Scenes/Menu.unity` — главное меню

## Зависимости

### Unity-пакеты (Packages/manifest.json)
- `com.github-glitchenzo.nugetforunity` — менеджер NuGet-пакетов
- `com.unity.textmeshpro`, `com.unity.ugui`, `com.unity.visualscripting`

### gRPC/Protobuf (Assets/Plugins/gRPC/, закоммичены в репо)
- `Google.Protobuf 3.21.12`
- `Grpc.Core 2.46.6`
- `Grpc.Core.Api 2.46.6`
- `System.Memory 4.5.3`
- `System.Runtime.CompilerServices.Unsafe 6.0.0`
- Нативные библиотеки gRPC для Windows/macOS/Linux

## Структура проекта

```
Assets/
├── Scripts/              # Логика игры и gRPC-сервер
│   ├── CarControllerAgent.cs     # Агент: награды, наблюдения, эпизоды
│   ├── UnityRacingGrpcServer.cs  # gRPC-сервер (Reset/Step)
│   ├── CarController.cs          # Физика автомобиля
│   ├── AIController.cs           # Автопилот по сплайну
│   ├── TrackCheckpoints.cs       # Система чекпоинтов
│   ├── CarSplineStats.cs         # Метрики относительно сплайна
│   ├── SplineCalculator.cs       # Генерация сплайна
│   └── UnityRacing/              # Сгенерированный protobuf/gRPC код
├── Proto/                # .proto определения
├── Scenes/               # Сцены Unity
├── Prefabs/              # Префабы машин и окружения
└── Plugins/              # DLL (устанавливаются NuGet)
Packages/                 # Unity-пакеты
_proto_gen/               # .NET проект для генерации protobuf-кода
```
