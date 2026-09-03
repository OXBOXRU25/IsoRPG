using UnityEditor;
using UnityEngine;
using IsoRPG.Combat;

namespace IsoRPG.EditorTools
{
    /// <summary>
    /// Манекен для проверки боевых анимаций.
    ///
    /// Просьба Павла 04.09.2026: «поставить рядом с героем куклу, которая не
    /// бьёт в ответ и не умирает». Без неё смотреть удары невозможно — живой
    /// кабан отвечает, отбегает и через десять секунд лежит трупом, а замах
    /// надо разглядывать по многу раз подряд.
    ///
    /// Манекен намеренно калека: у него есть только облик, цель и здоровье.
    /// Ни мозга, ни навигации, ни смерти — иначе он поведёт себя как монстр,
    /// а нам нужен неподвижный столб.
    ///
    /// Здоровье не бесконечное, а самовосстанавливающееся: полоска над
    /// головой должна дёргаться от ударов, иначе непонятно, попал ты или
    /// махнул мимо. Через полсекунды после удара она возвращается к полной.
    /// </summary>
    public static class TrainingDummy
    {
        private const string Name = "Манекен";

        public static void Apply()
        {
            var player = GameObject.Find("Player");
            if (player == null) { Debug.LogError("[IsoRPG] Героя нет."); return; }

            var old = GameObject.Find(Name);
            if (old != null) Object.DestroyImmediate(old);

            // Модель берём ту же, что у героя: она точно есть, точно нужного
            // размера и точно смотрится в нашем мире.
            var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/Player.prefab");

            if (source == null)
            {
                Debug.LogError("[IsoRPG] Нет Player.prefab — манекен не из чего собрать.");
                return;
            }

            var dummy = (GameObject)PrefabUtility.InstantiatePrefab(source);
            PrefabUtility.UnpackPrefabInstance(dummy, PrefabUnpackMode.Completely,
                                               InteractionMode.AutomatedAction);

            dummy.name = Name;

            // В трёх метрах перед героем — так, чтобы он попадал в кадр
            // вместе с ним и по нему можно было бить, не бегая.
            dummy.transform.position = player.transform.position + player.transform.forward * 3f;
            dummy.transform.rotation = Quaternion.LookRotation(-player.transform.forward);

            Strip(dummy);

            // Коллайдер — обязательно и первым делом.
            //
            // Снимая капсулу героя, я снял и его коллайдер: манекен остался
            // без тела, и по нему нельзя ни попасть лучом выбора, ни
            // ударить — Павлон 04.09.2026 «его нельзя ни выбрать, ни
            // ударить». Выбор цели и удар идут лучом по коллайдерам, а не
            // по мешам, и без него манекен для игры не существует.
            var body = dummy.GetComponent<CapsuleCollider>();
            if (body == null) body = dummy.AddComponent<CapsuleCollider>();

            body.radius = 0.35f;
            body.height = 1.8f;
            body.center = new Vector3(0f, 0.9f, 0f);
            body.isTrigger = false;

            Ground(dummy);

            var health = dummy.GetComponent<Health>();
            if (health == null) health = dummy.AddComponent<Health>();

            var target = dummy.GetComponent<Targetable>();
            if (target == null) target = dummy.AddComponent<Targetable>();

            target.Setup("Манекен", Faction.Hostile);

            dummy.AddComponent<IsoRPG.Combat.DummyHeal>();

            EditorSceneManager_MarkDirty();

            Debug.Log($"[IsoRPG] Манекен поставлен в трёх метрах перед героем. " +
                      $"Бьётся, не отвечает, не умирает.");
        }

        /// <summary>
        /// Поставить манекен на землю.
        ///
        /// Живого героя вниз тянет гравитация через капсулу, а у манекена её
        /// нет и быть не должно — он столб. Значит землю надо найти самим,
        /// лучом сверху: место перед героем может оказаться и ниже, и выше
        /// того, где стоит он сам, а висящая в воздухе кукла читается как
        /// поломка мира, а не как мишень.
        /// </summary>
        private static void Ground(GameObject go)
        {
            Vector3 from = go.transform.position + Vector3.up * 5f;

            if (!Physics.Raycast(from, Vector3.down, out var hit, 50f,
                                 ~0, QueryTriggerInteraction.Ignore))
            {
                Debug.LogWarning("[IsoRPG] Под манекеном не нашлось земли — оставлен где был.");
                return;
            }

            go.transform.position = hit.point;
        }

        /// <summary>
        /// Снять всё, что делает из модели живого участника.
        ///
        /// Перечисляем поимённо, а не «всё кроме нужного»: список компонентов
        /// героя растёт, и однажды в манекен уехало бы что-нибудь новое —
        /// например управление или камера.
        /// </summary>
        private static void Strip(GameObject go)
        {
            Kill<IsoRPG.Player.PlayerInputRouter>(go);
            Kill<IsoRPG.Player.ClickToMoveController>(go);
            Kill<IsoRPG.Player.KeyboardMove>(go);
            Kill<IsoRPG.Player.PlayerMotor>(go);
            Kill<IsoRPG.Player.JumpGesture>(go);
            Kill<IsoRPG.Player.IdleFidget>(go);
            Kill<IsoRPG.Player.HoverInspector>(go);
            Kill<IsoRPG.Player.AnimTryout>(go);
            Kill<MonsterBrain>(go);
            Kill<MeleeCombatant>(go);
            Kill<DeathHandler>(go);
            Kill<Respawner>(go);
            Kill<IsoRPG.Items.Equipment>(go);
            Kill<IsoRPG.Items.Inventory>(go);
            Kill<IsoRPG.Items.CharacterPreview>(go);
            Kill<UnityEngine.AI.NavMeshAgent>(go);
            Kill<CharacterController>(go);
            Kill<IsoRPG.UI.MouseCursor>(go);
            Kill<IsoRPG.Combat.HealthRegen>(go);

            // Интерфейс героя целиком: манекену не нужны ни полоски, ни окна.
            foreach (var canvas in go.GetComponentsInChildren<Canvas>(true))
                if (canvas != null) Object.DestroyImmediate(canvas.gameObject);
        }

        private static void Kill<T>(GameObject go) where T : Component
        {
            var component = go.GetComponent<T>();
            if (component != null) Object.DestroyImmediate(component, true);
        }

        private static void EditorSceneManager_MarkDirty()
            => UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
    }
}
