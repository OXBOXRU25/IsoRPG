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
        private const string RightSlotBone = "handslot.r";
        private const string LeftSlotBone = "handslot.l";

        [Tooltip("Модель в правой руке: оружие.")]
        [SerializeField] private GameObject rightHand;

        [Tooltip("Модель в левой руке: щит, второй клинок или пусто.")]
        [SerializeField] private GameObject leftHand;

        /// <summary>Задать модели из сборщика сцены.</summary>
        public void Setup(GameObject right, GameObject left)
        {
            rightHand = right;
            leftHand = left;
        }

        private void Start()
        {
            // Именно Start, а не Awake: модель персонажа — дочерний объект,
            // и в Awake иерархия может быть ещё не собрана целиком.
            Attach(rightHand, RightSlotBone);
            Attach(leftHand, LeftSlotBone);
        }

        private void Attach(GameObject model, string boneName)
        {
            if (model == null) return;

            var bone = FindBone(boneName);

            if (bone == null)
            {
                Debug.LogWarning($"[IsoRPG] У «{name}» нет кости {boneName} — " +
                                 "оружие показать некуда. Модель не из набора KayKit?");
                return;
            }

            var instance = Instantiate(model, bone);
            instance.name = "Held_" + model.name;
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
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
