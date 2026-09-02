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
        [SerializeField] private Vector3 gripAngles = new Vector3(6.47f, 93.00f, 178.34f);

        [Tooltip("То же для левой руки. Не копия правой: копия уводит клинок на 23 см вверх, нужно отражение.")]
        [SerializeField] private Vector3 gripLeft = new Vector3(0.0904f, 0.0060f, 0.0259f);

        [SerializeField] private Vector3 gripAnglesLeft = new Vector3(6.47f, 267.00f, 181.66f);

        /// <summary>
        /// Поставить хват числами из задания `grip-fit`.
        ///
        /// Задание считает доворот по матрице из Blender, а не по углам:
        /// покомпонентная перестановка углов между Blender и Unity не равна
        /// повороту, и именно она дала «кинжал повёрнут не в ту сторону».
        /// Ставить надо и в сцене тоже: значение, заданное и в коде, и в
        /// сцене, работает из сцены.
        /// </summary>
        public void SetGrip(Vector3 offset, Vector3 angles, Vector3 leftOffset, Vector3 leftAngles)
        {
            grip = offset;
            gripAngles = angles;
            gripLeft = leftOffset;
            gripAnglesLeft = leftAngles;
        }

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
            Show(EquipSlot.MainHand, rightSlot, ref rightModel, grip, gripAngles);

            // У левой руки свои числа: кисти зеркальны, и тот же локальный
            // трансформ кладёт в неё клинок иначе. До 02.09.2026 обе руки
            // получали одно и то же — второй кинжал был повёрнут не так, как
            // первый, хотя выглядело это как «оба кривые».
            Show(EquipSlot.OffHand, leftSlot, ref leftModel, gripLeft, gripAnglesLeft);
        }

        private void Show(EquipSlot slot, Transform bone, ref GameObject current,
                          Vector3 offset, Vector3 angles)
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
            // память проекта `dagger-grip-fit`) и пересчитаны заданием
            // `grip-fit` — по матрице, а не по углам. Прежний перенос
            // переставлял компоненты углов Эйлера, и это и было «кинжал
            // повёрнут не в ту сторону»: у Blender вертикаль Z и порядок
            // XYZ, у нас Y и ZXY, перестановка компонент повороту не равна.
            // Смотреть результат надо на боевой анимации, а не в покое:
            // в покое кисть висит иначе.
            current.transform.localPosition = offset;
            current.transform.localRotation = Quaternion.Euler(angles);
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
