using UnityEditor;
using UnityEditor.SceneManagement;
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
            OpenPlayableScene();

            var player = GameObject.Find("Player");
            if (player == null) { Debug.LogError("[IsoRPG] Героя нет."); return; }

            var old = GameObject.Find(Name);
            if (old != null) Object.DestroyImmediate(old);

            // Копируем ЖИВОГО героя из сцены, а не префаб.
            //
            // Павлон 04.09.2026: «ты это уже 1000 раз путаешь, у нашего героя
            // другая модель». Он прав, и щуп это подтвердил числами:
            // `Player.prefab` — вариант `SM_Chr_Commoner_Male_01` из
            // PolygonElvenRealm, шестнадцать мешей крестьян и рыцарей, а на
            // герое в сцене стоит ОДИН меш с материалом `Human-Custom2` и
            // аватаром `Human-Custom2Avatar`. Совсем другая модель.
            //
            // Отсюда правило, которое я нарушал: манекен обязан СПРАШИВАТЬ
            // героя, а не помнить, из чего тот сделан. Копия сцены переживёт
            // любую смену модели, ссылка на префаб — нет.
            // Собираем мишень ИЗ ПУСТОГО объекта, а не обстругиваем копию.
            //
            // Копия героя ловила нас четыре раза подряд, и каждый раз одним и
            // тем же: на ней оставался компонент, который делает из мишени
            // второго игрока. Сохранение (затирало прогресс), чтение клавиш
            // (приёмы уходили манекену), экран смерти (выскакивал от смерти
            // мишени), теперь урон бьёт по герою. Список снимаемого я писал по
            // памяти — и каждый раз забывал следующий.
            //
            // Обратный порядок закрывает весь класс разом: берём только МОДЕЛЬ
            // и добавляем ровно то, что мишени нужно. Лишнего не будет по
            // построению, а не по моей внимательности.
            var dummy = Object.Instantiate(player);

            dummy.name = Name;

            // Место — у лошади, а не перед героем.
            //
            // Павлон 04.09.2026: «не надо рядом, они стоят друг в друге,
            // поставь где-то недалеко от лошади и зафиксируй там». Три метра
            // перед героем оказались мало: при загрузке сохранения герой
            // появляется в другом месте и въезжает в манекен.
            //
            // Лошадь — хороший ориентир: она стоит на одном месте, её видно
            // издалека, и мишень рядом с ней читается как часть двора, а не
            // как предмет, забытый посреди поля.
            var horse = GameObject.Find("Лошадь");

            if (horse != null)
            {
                // В трёх метрах в сторону от лошади, чтобы не влезть в неё.
                dummy.transform.position = horse.transform.position + horse.transform.right * 3f;
                dummy.transform.rotation = Quaternion.LookRotation(-horse.transform.right);

                Debug.Log("[IsoRPG] Манекен поставлен у лошади.");
            }
            else
            {
                // Лошади нет — ставим перед героем, но подальше: пять метров
                // вместо трёх, иначе он снова окажется внутри.
                dummy.transform.position = player.transform.position + player.transform.forward * 5f;
                dummy.transform.rotation = Quaternion.LookRotation(-player.transform.forward);

                Debug.LogWarning("[IsoRPG] Лошади в сцене нет — манекен встал перед героем.");
            }

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

            Ground(dummy, player);

            var health = dummy.GetComponent<Health>();
            if (health == null) health = dummy.AddComponent<Health>();

            var target = dummy.GetComponent<Targetable>();
            if (target == null) target = dummy.AddComponent<Targetable>();

            target.Setup("Манекен", Faction.Hostile);

            // Оружие манекену выдаёт DummyHeal уже В ИГРЕ.
            //
            // Здесь это не работает и работать не может: у экипировки в
            // редакторе не вызван Awake, ссылка на сумку внутри неё пустая, и
            // надевание молча возвращает false — задание честно печатало
            // «клинков в руках 0» и выглядело исправным.
            dummy.AddComponent<IsoRPG.Combat.DummyHeal>();

            EditorSceneManager_MarkDirty();

            Debug.Log($"[IsoRPG] Манекен поставлен в трёх метрах перед героем. " +
                      $"Бьётся, не отвечает, не умирает.");
        }

        /// <summary>
        /// Открыть ту сцену, которая реально попадает в игру.
        ///
        /// В проекте две арены, и пакетный прогон по умолчанию открывает не
        /// ту: `Arena` вместо `ArenaAuthor`. Задание без явного открытия
        /// работало в случайной сцене и честно докладывало «готово» — а
        /// манекен оказывался там, куда игрок не попадает.
        ///
        /// Имя не вписываем: СПРАШИВАЕМ настройки сборки. Список сцен там
        /// один, и он же решает, что увидит игрок; своя копия имени разошлась
        /// бы с ним на первой же перестановке.
        /// </summary>
        private static void OpenPlayableScene()
        {
            string path = null;

            foreach (var entry in EditorBuildSettings.scenes)
            {
                if (!entry.enabled) continue;

                // Первая сцена — меню; нам нужна игровая, то есть следующая.
                if (entry.path.Contains("MainMenu")) continue;

                path = entry.path;
                break;
            }

            if (string.IsNullOrEmpty(path))
            {
                Debug.LogWarning("[IsoRPG] В настройках сборки нет игровой сцены — работаю в открытой.");
                return;
            }

            if (EditorSceneManager.GetActiveScene().path != path)
                EditorSceneManager.OpenScene(path, OpenSceneMode.Single);

            Debug.Log("[IsoRPG] Манекен ставлю в сцену игры: " + path);
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
        private static void Ground(GameObject go, GameObject player)
        {
            // Свои коллайдеры на время замера выключаем.
            //
            // Иначе луч сверху попадает В САМ манекен — я же только что дал
            // ему капсулу, — считает её землёй и ставит куклу на собственную
            // макушку. Павлон 04.09.2026 после первой попытки: «ты его ещё
            // выше поднял». Так и было: каждый прогон задания поднимал его
            // ещё на рост.
            var own = go.GetComponentsInChildren<Collider>(true);
            var was = new bool[own.Length];

            for (int i = 0; i < own.Length; i++)
            {
                was[i] = own[i].enabled;
                own[i].enabled = false;
            }

            // Отсчёт от ГЕРОЯ, а не от рельефа.
            //
            // Первая попытка искала землю лучом и нашла террейн на 2.65 —
            // а герой в мире автора стоит не на террейне, а на его земле,
            // которая ниже. Манекен честно встал на найденное и оказался в
            // трёх метрах над головой. Это тот же промах, что был у камеры:
            // пол здесь задаёт не рельеф, а физика мира автора.
            //
            // Герой же стоит на земле по определению — его туда опускает
            // капсула каждый кадр. Поэтому берём высоту у него, а лучом
            // только уточняем, если под ногами манекена опора нашлась НЕ
            // выше его самого.
            float ground = player == null ? go.transform.position.y : player.transform.position.y;

            Vector3 from = new Vector3(go.transform.position.x, ground + 2f, go.transform.position.z);

            bool found = Physics.Raycast(from, Vector3.down, out var hit, 8f,
                                         ~0, QueryTriggerInteraction.Ignore);

            for (int i = 0; i < own.Length; i++) own[i].enabled = was[i];

            // Берём высоту ГЕРОЯ, а луч — только справка в журнал.
            //
            // Вторая попытка доверяла лучу, если он нашёл опору не выше
            // героя, — и снова получила террейн: он лежит на 2.65, а герой
            // стоит на 3.22, потому что под ним земля автора. Манекен
            // утонул по колено. В трёх метрах перепад земли меньше, чем
            // разница между рельефом и настоящим полом, поэтому высота
            // героя тут — самый точный ответ, а не приближение.
            float y = ground;

            go.transform.position = new Vector3(go.transform.position.x, y, go.transform.position.z);

            Debug.Log($"[IsoRPG] Манекен на высоте {y:0.00} м (герой на {ground:0.00}); " +
                      $"луч {(found ? "нашёл «" + hit.collider.name + "» на " + hit.point.y.ToString("0.00") : "не нашёл ничего")}.");
        }

        /// <summary>
        /// Оставить на манекене ТОЛЬКО разрешённое, а не снимать запрещённое.
        ///
        /// Перечисление «что снять» подвело четыре раза подряд, и каждый раз
        /// одинаково: я писал список по памяти, а на копии героя оставался
        /// компонент, делающий из мишени второго игрока.
        ///
        ///  * `SaveService` — писал в файл героя и затирал его прогресс;
        ///  * `AbilityBook` — читал цифровые клавиши, и приёмы уходили манекену;
        ///  * `DeathScreen` — показывал экран смерти, когда умирал манекен;
        ///  * дальше урон по мишени стал бить по герою.
        ///
        /// Белый список закрывает весь класс сразу: что бы ни появилось у героя
        /// завтра, на манекен оно не переедет, потому что переезжает только
        /// названное здесь. Признак правильного списка — в нём НЕТ ничего, что
        /// читает ввод, пишет сохранение или показывает интерфейс.
        /// </summary>
        private static readonly System.Type[] Allowed =
        {
            typeof(Transform),
            typeof(Animator),
            typeof(SkinnedMeshRenderer),
            typeof(MeshRenderer),
            typeof(MeshFilter),
            typeof(CapsuleCollider),
            typeof(BoxCollider),
            typeof(SphereCollider),
            typeof(Health),
            typeof(Targetable),
            typeof(IsoRPG.Combat.DummyHeal),
            typeof(IsoRPG.Combat.HandAttachments),
            typeof(IsoRPG.Items.Equipment),
            typeof(IsoRPG.Items.Inventory),
            typeof(IsoRPG.Items.WeaponVisual),
            typeof(IsoRPG.World.JawLock),
        };

        private static void Strip(GameObject go)
        {
            // Идём по ВСЕЙ ветке: лишнее у героя висит и на корне, и на модели.
            var all = go.GetComponentsInChildren<Component>(true);

            int removed = 0;

            foreach (var component in all)
            {
                if (component == null) continue;

                var type = component.GetType();

                bool keep = false;

                foreach (var allowed in Allowed)
                {
                    if (allowed.IsAssignableFrom(type)) { keep = true; break; }
                }

                if (keep) continue;

                Object.DestroyImmediate(component);
                removed++;
            }

            // Интерфейс героя целиком: манекену не нужны ни полоски, ни окна.
            foreach (var canvas in go.GetComponentsInChildren<Canvas>(true))
                if (canvas != null) Object.DestroyImmediate(canvas.gameObject);

            Debug.Log($"[IsoRPG] С манекена снято лишних компонентов: {removed}. " +
                      $"Оставлено только разрешённое — ни ввода, ни сохранения, ни интерфейса.");
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
