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
        private const string RightSlotBone = "handslot.r";
        private const string LeftSlotBone = "handslot.l";

        [SerializeField] private Equipment equipment;

        private Transform rightSlot;
        private Transform leftSlot;
        private GameObject rightModel;
        private GameObject leftModel;

        private void Awake()
        {
            if (equipment == null) equipment = GetComponent<Equipment>();

            rightSlot = FindBone(RightSlotBone);
            leftSlot = FindBone(LeftSlotBone);

            // Молчать тут нельзя: без кости оружие просто не появится, и
            // снаружи это неотличимо от «модель не назначена» или «предмет
            // не надет». Три разные причины с одним симптомом — худший вид
            // отладки.
            if (rightSlot == null && leftSlot == null)
            {
                Debug.LogWarning($"[IsoRPG] У «{name}» нет костей {RightSlotBone} и {LeftSlotBone} — " +
                                 "оружие показать некуда. Модель не из набора KayKit?");
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
            current.transform.localPosition = Vector3.zero;
            current.transform.localRotation = Quaternion.identity;
            current.transform.localScale = Vector3.one;

            // Коллайдеры у оружия снимаем: клик по земле рядом с персонажем
            // иначе попадает в лезвие, и он никуда не идёт. Тот же класс
            // ошибки, что был с коллайдером самого игрока.
            foreach (var collider in current.GetComponentsInChildren<Collider>())
                Destroy(collider);
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
    }
}
