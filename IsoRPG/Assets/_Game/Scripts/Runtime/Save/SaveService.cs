using UnityEngine;
using IsoRPG.Combat;
using IsoRPG.Items;
using IsoRPG.Progression;
using IsoRPG.Quests;

namespace IsoRPG.Save
{
    /// <summary>
    /// Фоновое сохранение персонажа.
    ///
    /// Кнопки «Сохранить» нет и не будет: игра метит в сетевую, а там
    /// состояние пишется само, и игрок просто заходит и продолжает. Кнопка
    /// сохранения — привычка одиночных игр, и завести её сейчас значит
    /// выкинуть потом вместе с половиной кода вокруг.
    ///
    /// Пишем по двум поводам сразу, и это не перестраховка:
    ///
    ///   — по СОБЫТИЮ (уровень, вещь, сданный квест) — чтобы не потерять то,
    ///     что игрок считает достижением;
    ///   — по ТАЙМЕРУ — чтобы не потерять положение и здоровье между
    ///     событиями: между двумя находками можно полчаса ходить по лесу.
    ///
    /// События приходят пачками — надел вещь, и сумка с экипировкой дёрнулись
    /// вместе. Поэтому запись откладывается на пару секунд и склеивается в
    /// одну: писать файл трижды за кадр незачем.
    /// </summary>
    public sealed class SaveService : MonoBehaviour
    {
        /// <summary>Через сколько после события писать. Склеивает пачки.</summary>
        private const float EventDelay = 2f;

        /// <summary>Как часто писать просто так — положение, здоровье.</summary>
        private const float Heartbeat = 45f;

        public static SaveService Instance { get; private set; }

        private ISaveBackend backend;

        private Experience experience;
        private Health health;
        private ResourcePool energy;
        private Inventory inventory;
        private Equipment equipment;
        private TalentBook talents;
        private QuestLog quests;

        private float writeAt = -1f;
        private float nextHeartbeat;
        private bool loading;

        /// <summary>Состояние мира — общее, живёт рядом с персонажем.</summary>
        private WorldState world = new WorldState();

        private void Awake()
        {
            Instance = this;
            backend = new FileSaveBackend();

            experience = GetComponent<Experience>();
            health = GetComponent<Health>();
            energy = GetComponent<ResourcePool>();
            inventory = GetComponent<Inventory>();
            equipment = GetComponent<Equipment>();
            talents = GetComponent<TalentBook>();
            quests = GetComponent<QuestLog>();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Start()
        {
            // Грузим в Start: к этому моменту стартовое снаряжение уже выдано,
            // и наше состояние ложится поверх него, а не под него.
            LoadNow();

            nextHeartbeat = Time.time + Heartbeat;
            Subscribe();
        }

        private void OnApplicationQuit()
        {
            // Последняя запись при выходе — синхронно и без отсрочки: игру
            // закрывают в тот же кадр, отложенной записи не дождаться.
            WriteNow();
        }

        private void OnApplicationPause(bool paused)
        {
            // На мобильных выход часто происходит без Quit вовсе.
            if (paused) WriteNow();
        }

        private void Update()
        {
            if (loading) return;

            if (writeAt > 0f && Time.time >= writeAt)
            {
                writeAt = -1f;
                WriteNow();
            }

            if (Time.time >= nextHeartbeat)
            {
                nextHeartbeat = Time.time + Heartbeat;
                WriteNow();
            }
        }

        // ------------------------------------------------------------------

        /// <summary>Запросить запись. Несколько запросов подряд станут одной.</summary>
        public void RequestWrite()
        {
            if (loading) return;

            if (writeAt < 0f) writeAt = Time.time + EventDelay;
        }

        /// <summary>Отметить событие мира: выданную награду, открытый сундук.</summary>
        public void MarkRewardClaimed(string key)
        {
            if (string.IsNullOrEmpty(key) || world.claimedRewards.Contains(key)) return;

            world.claimedRewards.Add(key);
            RequestWrite();
        }

        public bool IsRewardClaimed(string key) => world.claimedRewards.Contains(key);

        public void MarkChestOpened(string key)
        {
            if (string.IsNullOrEmpty(key) || world.openedChests.Contains(key)) return;

            world.openedChests.Add(key);
            RequestWrite();
        }

        public bool IsChestOpened(string key) => world.openedChests.Contains(key);

        /// <summary>Начать заново: стереть файл и не писать до перезапуска.</summary>
        public static void EraseSave()
        {
            new FileSaveBackend().Erase();
        }

        public static bool HasSave => new FileSaveBackend().HasSave;

        // ------------------------------------------------------------------

        private void WriteNow()
        {
            if (loading || backend == null) return;

            var data = new SaveFile { character = Capture(), world = world };
            backend.Write(data);
        }

        private CharacterState Capture()
        {
            var state = new CharacterState();

            if (experience != null)
            {
                state.level = experience.Level;
                state.experience = experience.Current;
            }

            if (health != null) state.health = health.Current;
            if (energy != null) state.energy = energy.Current;

            if (inventory != null)
            {
                state.gold = inventory.Gold;
                state.bag.AddRange(inventory.CaptureState());
            }

            if (equipment != null) state.worn = equipment.CaptureState();
            if (talents != null) state.talents = talents.CaptureState();
            if (quests != null) state.quests = quests.CaptureState();

            var at = transform.position;
            state.x = at.x;
            state.y = at.y;
            state.z = at.z;

            return state;
        }

        private void LoadNow()
        {
            if (backend == null || !backend.HasSave) return;

            loading = true;

            backend.Read(data =>
            {
                if (data != null) Apply(data);

                loading = false;
            });
        }

        private void Apply(SaveFile data)
        {
            world = data.world ?? new WorldState();

            var state = data.character;
            if (state == null) return;

            // Порядок важен: сперва уровень (от него зависят очки талантов и
            // требования вещей), потом снаряжение, и только потом здоровье —
            // иначе прибавка к запасу от талантов и брони перезапишет его.
            if (experience != null) experience.RestoreState(state.level, state.experience);
            if (talents != null) talents.RestoreState(state.talents);

            if (inventory != null) inventory.RestoreState(state.bag, state.gold);
            if (equipment != null) equipment.RestoreState(state.worn);

            if (quests != null) quests.RestoreState(state.quests);

            if (health != null && state.health > 0) health.RestoreState(state.health);
            if (energy != null && state.energy > 0) energy.RestoreState(state.energy);

            MoveTo(new Vector3(state.x, state.y, state.z));

            CombatLog.Add("Игра продолжена.", LogKind.System);
        }

        /// <summary>
        /// Поставить героя на сохранённое место.
        ///
        /// Через агента навигации, а не присваиванием позиции: агент держит
        /// собственную координату и в следующем же кадре вернёт персонажа
        /// туда, где считал его находящимся.
        /// </summary>
        private void MoveTo(Vector3 point)
        {
            if (point.sqrMagnitude < 0.01f) return;

            var agent = GetComponent<UnityEngine.AI.NavMeshAgent>();

            if (agent != null && agent.enabled) agent.Warp(point);
            else transform.position = point;
        }

        private void Subscribe()
        {
            // Подписываемся на всё, что игрок считает своим достижением.
            if (inventory != null) inventory.Changed += RequestWrite;
            if (equipment != null) equipment.Changed += RequestWrite;
            if (talents != null) talents.Changed += RequestWrite;
            if (quests != null) quests.Changed += RequestWrite;

            if (experience != null) experience.LevelUp += OnLevelUp;
        }

        private void OnLevelUp(int level) => RequestWrite();
    }
}
