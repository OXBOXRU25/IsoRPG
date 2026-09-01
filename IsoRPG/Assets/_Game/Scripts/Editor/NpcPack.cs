using System.Linq;
using IsoRPG.Combat;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace IsoRPG.EditorTools
{
    /// <summary>
    /// Ставит одного мирного NPC — тем же Sidekick-конструктором, что и
    /// герой (<c>Starter_01</c>, рыцарь с красным плюмажем). Стоит на месте
    /// в тихой стойке и поворачивается лицом к герою, когда тот подходит —
    /// готовым компонентом <see cref="IsoRPG.Items.FacePlayer"/>, тем же,
    /// что уже стоит на торговцах и квестодателях, а не новым дублем.
    /// Вне радиуса смотрит в свою сторону по умолчанию.
    /// </summary>
    public static class NpcPack
    {
        private const string PrefabPath =
            "Assets/Synty/SidekickCharacters/Characters/Starter/Starter_01/Starter_01.prefab";
        private const string GroupName = "Талин Кини";   // имя объекта = имя в диалоге (QuestGiver.DisplayName)

        /// <summary>
        /// Имя в игре. Оно же ключ к портрету в справочнике — менять только
        /// вместе с записью в Portraits, иначе круг портрета останется пустым,
        /// причём молча.
        /// </summary>
        private const string PersonName = "Талин Кини";

        private const string IdleClipPath =
            "Assets/Synty/AnimationBaseLocomotion/Animations/Sidekick/Masculine/Idles/A_MOD_BL_Idle_Standing_Masc.fbx";
        private const string ControllerPath =
            "Assets/_Game/Art/Animations/Controllers/AC_Npc_Idle.controller";

        // Точка — ровно та, где стоял Павлон на кадре 31.08.2026, лицом к
        // котелку. Координаты не с глазомера: они взяты из его сохранения
        // (`character.json`, блок с x/y/z), то есть это буквально его место.
        private static readonly Vector2 Spot = new Vector2(51.73f, -28.23f);

        // Запасной разворот, если костровище в сцене не найдётся. Обычно
        // угол считается по нему же — см. FaceHearth ниже.
        private const float FacingYaw = 76f;

        /// <summary>Костровище, на которое смотрит дозорный.</summary>
        private const string HearthMesh = "SM_Prop_Camp_Fireplace_01";

        /// <summary>
        /// Куда смотреть: на котелок.
        ///
        /// Угол считаем по самому костровищу, а не держим числом. Число
        /// разъедется молча, если лагерь когда-нибудь подвинут, — и дозорный
        /// останется смотреть в пустоту, что заметно сразу, а причина нет.
        /// </summary>
        private static float FaceHearth(Vector3 from)
        {
            foreach (var mf in UnityEngine.Object.FindObjectsByType<MeshFilter>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (mf.sharedMesh == null || mf.sharedMesh.name != HearthMesh) continue;

                Vector3 to = mf.transform.position - from;
                to.y = 0f;

                if (to.sqrMagnitude < 0.01f) break;

                float yaw = Quaternion.LookRotation(to).eulerAngles.y;
                Debug.Log("[IsoRPG] Дозорный развёрнут на котелок: " + yaw.ToString("0") + "°.");
                return yaw;
            }

            Debug.LogWarning("[IsoRPG] Костровище не найдено — беру запасной угол " + FacingYaw + "°.");
            return FacingYaw;
        }

        [MenuItem("Tools/IsoRPG/Мир: поставить дозорного NPC", priority = 42)]
        public static void Build()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[IsoRPG] В режиме Play изменения не сохранятся.");
                return;
            }

            var controller = BuildIdleController();
            if (controller == null) return;

            var source = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);

            if (source == null)
            {
                Debug.LogError("[IsoRPG] Не найден префаб NPC: " + PrefabPath);
                return;
            }

            var terrain = Object.FindObjectsByType<Terrain>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None).FirstOrDefault();

            if (terrain == null)
            {
                Debug.LogError("[IsoRPG] Террейна нет — не на что ставить NPC.");
                return;
            }

            var old = GameObject.Find(GroupName);
            if (old != null) Object.DestroyImmediate(old);

            // И под прежним именем: NPC звался «НПС Дозорный», пока Павлон
            // не дал ему имя. Без этой строки в сцене осталось бы двое.
            var older = GameObject.Find("НПС Дозорный");
            if (older != null) Object.DestroyImmediate(older);

            float y = terrain.SampleHeight(new Vector3(Spot.x, 0f, Spot.y)) +
                      terrain.transform.position.y;

            if (NavMesh.SamplePosition(new Vector3(Spot.x, y, Spot.y), out var hit, 6f, NavMesh.AllAreas))
                y = hit.position.y;

            // Без прослойки «Наклон»/GroundAlign: NPC стоит на ровной траве,
            // склон конформить незачем.
            var npc = new GameObject(GroupName);
            npc.transform.position = new Vector3(Spot.x, y, Spot.y);
            npc.transform.rotation = Quaternion.Euler(0f, FaceHearth(npc.transform.position), 0f);
            npc.AddComponent<IsoRPG.World.NpcTurnToHero>();

            var model = (GameObject)PrefabUtility.InstantiatePrefab(source);
            model.name = "Модель";
            model.transform.SetParent(npc.transform, false);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;

            var parts = model.GetComponentsInChildren<Renderer>(true);
            float lift = 0f;

            if (parts.Length > 0)
            {
                var box = parts[0].bounds;
                foreach (var part in parts) box.Encapsulate(part.bounds);

                float groundY = terrain.SampleHeight(new Vector3(Spot.x, 0f, Spot.y)) +
                                terrain.transform.position.y;

                lift = groundY - box.min.y;
                model.transform.localPosition = new Vector3(0f, lift, 0f);
            }

            var animator = model.GetComponentInChildren<Animator>();
            if (animator == null) animator = model.AddComponent<Animator>();

            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;

            var body = npc.AddComponent<CapsuleCollider>();
            // Пошире силуэта — клик по краю модели (плечи, оружие) промахивался.
            body.height = 2.0f;
            body.radius = 0.6f;
            body.center = new Vector3(0f, 0.95f, 0f);

            var targetable = npc.AddComponent<Targetable>();
            targetable.Setup(PersonName, Faction.Neutral);

            var health = npc.AddComponent<Health>();
            health.Setup(999);

            // Квест и знак над головой. Знак строит сам QuestGiver: жёлтый
            // восклицательный — есть работа, вопросительный — пора сдавать.
            QuestBuilder.Build();

            var quest = QuestBuilder.LoadBoarHunt();

            if (quest == null)
            {
                Debug.LogError("[IsoRPG] Квест охоты не собрался — NPC останется без задания.");
            }
            else
            {
                var giver = npc.AddComponent<IsoRPG.Quests.QuestGiver>();

                // Голос: пара фраз при обращении, случайно и с откатом.
                // Клипы лежат в ресурсах, компонент подхватывает их сам.
                npc.AddComponent<AudioSource>();
                npc.AddComponent<IsoRPG.Quests.NpcVoice>();
                giver.SetupMarkerMaterial(SandboxSceneBuilder.MarkerMaterial());
                giver.Setup(quest);
                EditorUtility.SetDirty(giver);

                Debug.Log("[IsoRPG] Квест «" + quest.title + "» повешен на " + PersonName +
                          ": нужно " + quest.requiredCount + " шт. «" +
                          (quest.requiredItem != null ? quest.requiredItem.displayName : "НЕТ ПРЕДМЕТА") + "».");
            }

            EditorUtility.SetDirty(npc);
            EditorSceneManager.MarkAllScenesDirty();

            Debug.Log("[IsoRPG] NPC поставлен на " + npc.transform.position.ToString("0.0") +
                      ", смотрит на " + FacingYaw + "°, подъём модели " + lift.ToString("0.00") +
                      " м. Повернётся к герою при подходе ближе 15 м.");
        }

        /// <summary>
        /// Минимальный контроллер: только стойка, зациклена. NPC не ходит и
        /// не дерётся — незачем тащить полный боевой набор параметров.
        /// </summary>
        private static AnimatorController BuildIdleController()
        {
            var idle = AssetDatabase.LoadAllAssetsAtPath(IdleClipPath)
                                    .OfType<AnimationClip>()
                                    .FirstOrDefault(c => !c.name.StartsWith("__preview"));

            if (idle == null)
            {
                Debug.LogError("[IsoRPG] Клип стойки NPC не найден: " + IdleClipPath);
                return null;
            }

            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath) != null)
                AssetDatabase.DeleteAsset(ControllerPath);

            var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            var state = controller.layers[0].stateMachine.AddState("Idle");
            state.motion = idle;
            controller.layers[0].stateMachine.defaultState = state;

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();

            return controller;
        }
    }
}
