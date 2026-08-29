using UnityEngine;

namespace IsoRPG.World
{
    /// <summary>
    /// Разворачивает объект к камере каждый кадр.
    ///
    /// Нужен подписям в мире: номер над головой, имя, любая табличка. Без
    /// разворота половина надписей окажется к игроку боком или задом, и
    /// прочитать их нельзя — а смысл подписи ровно в том, чтобы её читали.
    ///
    /// Камеру ищем один раз и запоминаем: <c>Camera.main</c> внутри каждого
    /// кадра у каждой подписи — это поиск по всей сцене по тегу, и на сотне
    /// объектов он заметен в профиле.
    /// </summary>
    public sealed class FaceCamera : MonoBehaviour
    {
        private Transform target;

        private void LateUpdate()
        {
            if (target == null)
            {
                var camera = Camera.main;
                if (camera == null) return;

                target = camera.transform;
            }

            // Поворачиваем ОТ камеры, а не к ней: текст в Unity читается со
            // стороны своей нормали, и разворот «лицом» покажет его зеркально.
            transform.rotation = Quaternion.LookRotation(
                transform.position - target.position, Vector3.up);
        }
    }
}
