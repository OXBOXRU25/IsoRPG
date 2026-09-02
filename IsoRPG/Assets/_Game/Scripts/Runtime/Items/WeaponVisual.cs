using UnityEngine;

namespace IsoRPG.Items
{
    /// <summary>
    /// Показывает надетое оружие в руках персонажа.
    ///
    /// До сих пор экипировка меняла только числа: надел кинжал — вырос урон,
    /// снял — упал. Проверить это можно было лишь в окне персонажа, а на
    /// экране человек всё время дрался пустыми руками. Из-за этого и анимация
    /// удара двумя клинками читалась как размахивание руками.
    ///
    /// Модель берётся из описания предмета и вкладывается в кость-держатель.
    /// У набора KayKit для этого заведены отдельные кости handslot.l и
    /// handslot.r — они не участвуют в деформации тела и существуют ровно
    /// затем, чтобы к ним что-то цеплять.
    /// </summary>
    public sealed class WeaponVisual : MonoBehaviour
    {
        /// <summary>
        /// Кости-держатели, по порядку поиска.
        ///
        /// У KayKit это `handslot.r`, у Sidekick таких нет вовсе — там
        /// берём саму кисть `hand_r`. До 02.09.2026 искали только первую,
        /// и у нашего героя оружие не появлялось никогда: в журнале честно
        /// висело «нет костей handslot.r и handslot.l».
        /// </summary>
        [Header("Посадка в руке")]
        [Tooltip("Смещение оружия относительно кости, метры. Ноль годится только для костей-держателей KayKit.")]
        [SerializeField] private Vector3 grip = new Vector3(-0.0904f, 0.0060f, 0.0259f);

        [Tooltip("Доворот оружия в кости, градусы.")]
        [SerializeField] private Vector3 gripAngles = new Vector3(-96.5f, -93.2f, -1.7f);

        private static readonly string[] RightSlotBones = { "handslot.r", "prop_r", "hand_r" };
        private static readonly string[] LeftSlotBones = { "handslot.l", "prop_l", "hand_l" };

        [SerializeField] private Equipment equipment;

        /// <summary>
        /// Слой, на который класть созданное оружие. Нужен витрине в окне
        /// снаряжения: её модель живёт на своём слое, и кинжал, оставшийся
        /// на слое по умолчанию, попал бы в кадр основной камеры — висящим
        /// в воздухе далеко под картой.
        /// </summary>
        private int forcedLayer = -1;

        /// <summary>
        /// Показывать чужую экипировку. Копии героя в окне отдают ту же
        /// самую — поэтому синхронизировать нечего: источник один.
        /// </summary>
        public void Setup(Equipment source, int layer = -1)
        {
            equipment = source;
            forcedLayer = layer;
        }

        private Transform rightSlot;
        private Transform leftSlot;
        private GameObject rightModel;
        private GameObject leftModel;

        private void Awake()
        {
            if (equipment == null) equipment = GetComponent<Equipment>();

            rightSlot = FindAnyBone(RightSlotBones);
            leftSlot = FindAnyBone(LeftSlotBones);

            // Молчать тут нельзя: без кости оружие просто не появится, и
            // снаружи это неотличимо от «модель не назначена» или «предмет
            // не надет». Три разные причины с одним симптомом — худший вид
            // отладки.
            if (rightSlot == null && leftSlot == null)
            {
                Debug.LogWarning($"[IsoRPG] У «{name}» нет ни одной кости-держателя " +
                                 $"({string.Join(", ", RightSlotBones)}) — оружие показать некуда.");
            }
        }

        private void OnEnable()
        {
            if (equipment != null) equipment.Changed += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            if (equipment != null) equipment.Changed -= Refresh;
        }

        private void Refresh()
        {
            Show(EquipSlot.MainHand, rightSlot, ref rightModel);
            Show(EquipSlot.OffHand, leftSlot, ref leftModel);
        }

        private void Show(EquipSlot slot, Transform bone, ref GameObject current)
        {
            // Старую модель снимаем всегда, даже если новой не будет: иначе
            // снятый кинжал останется висеть в руке.
            if (current != null)
            {
                Destroy(current);
                current = null;
            }

            if (bone == null || equipment == null) return;

            var stack = equipment.GetSlot(slot);
            if (stack.IsEmpty || stack.Item == null || stack.Item.worldModel == null) return;

            current = Instantiate(stack.Item.worldModel, bone);
            current.name = "Weapon_" + slot;

            // Посадка в руке.
            //
            // Нули стояли под кости-держатели KayKit: у них своя ось уже
            // развёрнута под оружие, и доворот не нужен. У Sidekick мы цепляем
            // за саму кисть `hand_r`, а у неё ось идёт вдоль пальцев — клинок
            // с нулями торчит поперёк ладони.
            //
            // Числа сняты примеркой в Blender (соседний чат, 02.09.2026,
            // память проекта `dagger-grip-fit`) и пересчитаны под Unity: у
            // Blender вертикаль Z, у нас Y, поэтому оси переставлены.
            // Подбираются глазом на боевой анимации, а не в позе покоя:
            // в покое кисть висит иначе.
            current.transform.localPosition = grip;
            current.transform.localRotation = Quaternion.Euler(gripAngles);
            current.transform.localScale = Vector3.one;

            // Коллайдеры у оружия снимаем: клик по земле рядом с персонажем
            // иначе попадает в лезвие, и он никуда не идёт. Тот же класс
            // ошибки, что был с коллайдером самого игрока.
            foreach (var collider in current.GetComponentsInChildren<Collider>())
                Destroy(collider);

            if (forcedLayer >= 0)
                foreach (var child in current.GetComponentsInChildren<Transform>(true))
                    child.gameObject.layer = forcedLayer;
        }

        /// <summary>
        /// Ищет кость по имени во всей иерархии, включая выключенные ветки.
        /// </summary>
        private Transform FindBone(string boneName)
        {
            foreach (var t in GetComponentsInChildren<Transform>(true))
                if (t.name == boneName) return t;

            return null;
        }

        /// <summary>Первая найденная кость из списка: наборы называют её по-разному.</summary>
        private Transform FindAnyBone(string[] names)
        {
            foreach (var boneName in names)
            {
                var bone = FindBone(boneName);
                if (bone != null) return bone;
            }

            return null;
        }
    }
}
