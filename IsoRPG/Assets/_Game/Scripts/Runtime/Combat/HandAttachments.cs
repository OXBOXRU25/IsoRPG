using UnityEngine;

namespace IsoRPG.Combat
{
    /// <summary>
    /// Вкладывает заданные модели в руки существа.
    ///
    /// Отличается от экипировки игрока намеренно: у монстра нет ни сумки, ни
    /// характеристик предметов, и заводить их ради двух моделей значило бы
    /// строить систему под задачу, которой нет. Здесь оружие — часть облика,
    /// а не имущества: что положили при сборке, то и держит.
    ///
    /// Крепим в кости-держатели набора KayKit (handslot.l и handslot.r). Они
    /// не участвуют в деформации тела и существуют ровно затем, чтобы к ним
    /// что-то цеплять.
    /// </summary>
    public sealed class HandAttachments : MonoBehaviour
    {
        /// <summary>
        /// Куда цеплять оружие, в порядке предпочтения.
        ///
        /// Наборы называют кости-держатели по-своему: у KayKit это
        /// `handslot.r`, у Sidekick — `prop_r`. Обе не участвуют в
        /// деформации тела и существуют ровно затем, чтобы к ним что-то
        /// цеплять. Кисть `hand_r` идёт последней запаской: к ней тоже
        /// можно прицепить, но она гнётся вместе с пальцами, и предмет
        /// слегка ездит.
        /// </summary>
        private static readonly string[] RightSlotBones = { "handslot.r", "prop_r", "hand_r" };
        private static readonly string[] LeftSlotBones = { "handslot.l", "prop_l", "hand_l" };

        [Tooltip("Модель в правой руке: оружие.")]
        [SerializeField] private GameObject rightHand;

        [Tooltip("Доворот оружия в держателе, градусы. Подбирается на кадре.")]
        [SerializeField] private Vector3 gripRotation = new Vector3(0f, 0f, 90f);

        [Tooltip("Смещение рукояти в держателе, метры.")]
        [SerializeField] private Vector3 gripOffset = Vector3.zero;

        [Tooltip("Модель в левой руке: щит, второй клинок или пусто.")]
        [SerializeField] private GameObject leftHand;

        /// <summary>Задать модели из сборщика сцены.</summary>
        public void Setup(GameObject right, GameObject left)
        {
            rightHand = right;
            leftHand = left;
        }

        /// <summary>
        /// Задать посадку оружия в держателе.
        ///
        /// Нужен отдельным вызовом, потому что значения по умолчанию из кода
        /// достаются только НОВОМУ компоненту. У героя, которому руки уже
        /// добавили раньше, лежит старое сериализованное число, и правка
        /// умолчания на него не действует — снаружи это выглядит как «код
        /// поменял, а в игре ничего не изменилось».
        /// </summary>
        public void SetGrip(Vector3 rotation, Vector3 offset)
        {
            gripRotation = rotation;
            gripOffset = offset;
        }

        private void Start()
        {
            // Именно Start, а не Awake: модель персонажа — дочерний объект,
            // и в Awake иерархия может быть ещё не собрана целиком.
            Attach(rightHand, RightSlotBones);
            Attach(leftHand, LeftSlotBones);
        }

        private void Attach(GameObject model, string[] boneNames)
        {
            if (model == null) return;

            Transform bone = null;

            foreach (var boneName in boneNames)
            {
                bone = FindBone(boneName);
                if (bone != null) break;
            }

            if (bone == null)
            {
                Debug.LogWarning($"[IsoRPG] У «{name}» нет ни одной кости-держателя " +
                                 $"({string.Join(", ", boneNames)}) — оружие показать некуда.");
                return;
            }

            var instance = Instantiate(model, bone);
            instance.name = "Held_" + model.name;

            // Посадка оружия в кости.
            //
            // Раньше стояло «ноль поворота, ноль смещения» — и клинок висел
            // как попало: у кости-держателя своя ось, у модели оружия своя,
            // и совпадают они только случайно. Оружие Synty нарисовано
            // остриём вдоль +Y, а держатель `prop_r` смотрит вдоль кости, —
            // отсюда доворот. Числа подбираются глазом на живом кадре, и
            // это нормально: аналитически их не вывести, наборы рисовали
            // разные люди.
            instance.transform.localPosition = gripOffset;
            instance.transform.localRotation = Quaternion.Euler(gripRotation);
            instance.transform.localScale = Vector3.one;

            // Коллайдеры снимаем: иначе клик по земле рядом с монстром
            // попадает в лезвие, а сканирование целей может зацепить оружие
            // вместо его владельца.
            foreach (var collider in instance.GetComponentsInChildren<Collider>())
                Destroy(collider);
        }

        private Transform FindBone(string boneName)
        {
            foreach (var t in GetComponentsInChildren<Transform>(true))
                if (t.name == boneName) return t;

            return null;
        }
    }
}
