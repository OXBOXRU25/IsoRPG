using UnityEngine;

namespace IsoRPG.World
{
    /// <summary>
    /// Мирный NPC поворачивается лицом к герою, когда тот подходит, и
    /// возвращается к исходному развороту, когда герой отходит.
    /// </summary>
    public sealed class NpcTurnToHero : MonoBehaviour
    {
        [SerializeField] private float noticeRange = 15f;
        [SerializeField] private float turnSpeed = 220f;

        private Transform hero;
        private Quaternion home;

        private void Awake()
        {
            home = transform.rotation;
        }

        private void Update()
        {
            if (hero == null)
            {
                var go = GameObject.Find("Player");
                if (go != null) hero = go.transform;
                if (hero == null) return;
            }

            Vector3 to = hero.position - transform.position;
            to.y = 0f;

            bool near = to.sqrMagnitude < noticeRange * noticeRange;
            Quaternion wanted = (near && to.sqrMagnitude > 0.01f)
                ? Quaternion.LookRotation(to.normalized, Vector3.up)
                : home;

            transform.rotation = Quaternion.RotateTowards(transform.rotation, wanted, turnSpeed * Time.deltaTime);
        }
    }
}
