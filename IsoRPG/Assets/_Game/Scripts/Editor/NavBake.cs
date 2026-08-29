using Unity.AI.Navigation;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace IsoRPG.EditorTools
{
    /// <summary>
    /// Выпечка навигации — одна на весь проект.
    ///
    /// Раньше эта же дюжина строк лежала копией в каждом сборщике: в замене
    /// деревьев, в крае мира, в витрине персонажей, в починке проходимости.
    /// Копии разошлись ровно так, как расходятся копии: половина пекла по
    /// нарисованным мешам, половина не пекла вовсе, и какой из вариантов
    /// подействует, зависело от того, какой пункт меню нажали последним.
    ///
    /// <b>Печём по КОЛЛАЙДЕРАМ, а не по нарисованным мешам.</b> Это главное
    /// решение, и оно оплачено невидимой стеной вокруг деревьев.
    ///
    /// Нарисованный меш дерева — это крона в двадцать метров, и по ней
    /// навигация вырезала круг, куда не ступить. Мы обошли это, исключив
    /// деревья из выпечки и поставив каждому карвящее препятствие по
    /// «стволу» — но радиус препятствия взяли долей от габарита кроны и
    /// упёрлись в собственный предел 2.5 м. Получился невидимый столб пяти
    /// метров в поперечнике вокруг метрового ствола: игрок останавливается в
    /// чистом поле и не понимает, обо что.
    ///
    /// По коллайдерам всё это лишнее. У дерева коллайдер — капсула по
    /// стволу, и навигация обходит ровно ствол. Ни исключений из выпечки, ни
    /// сотен карвящих препятствий, которые вдобавок пересчитываются каждый
    /// кадр.
    ///
    /// Цена решения названа честно: объект БЕЗ коллайдера навигацию больше не
    /// перекрывает. Поэтому после переезда окружения проверять щупом
    /// «Невидимая стена», не появилось ли проходимых стен.
    /// </summary>
    public static class NavBake
    {
        public static void Rebake()
        {
            // Печём на террейне, если он есть.
            //
            // Раньше поверхность навигации висела на объекте «Ground» —
            // плоском листе, который мы давно заменили террейном. Поверхность
            // осталась на нём же и продолжала собирать геометрию оттуда, где
            // земли уже нет. Террейн — это и есть пол; на нём поверхности и
            // место.
            var terrain = Object.FindObjectsByType<Terrain>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None).FirstOrDefault();

            var ground = terrain != null ? terrain.gameObject : GameObject.Find("Ground");

            if (ground == null)
            {
                Debug.LogWarning("[IsoRPG] Нет ни террейна, ни объекта Ground — " +
                                 "навигацию не на чем печь.");
                return;
            }

            // Старую поверхность на прежней земле снимаем: две поверхности
            // строят две сетки, и агент выберет ту, что окажется выше.
            var stale = GameObject.Find("Ground");

            if (stale != null && stale != ground)
            {
                foreach (var old in stale.GetComponents<NavMeshSurface>())
                {
                    Object.DestroyImmediate(old);
                    Debug.Log("[IsoRPG] Снята прежняя поверхность навигации с «Ground».");
                }
            }

            var surface = ground.GetComponent<NavMeshSurface>();

            if (surface == null)
            {
                surface = ground.AddComponent<NavMeshSurface>();
                surface.collectObjects = CollectObjects.All;
            }

            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.BuildNavMesh();

            EditorUtility.SetDirty(surface);

            // Отчитываемся числами, а не фактом вызова.
            //
            // BuildNavMesh не бросает исключений и не ругается: если печь
            // было не из чего, он честно отработает и оставит пусто. Дальше
            // все стоят на месте, а в журнале ни строчки. Печатаем размер
            // готовой сетки — по нему сразу видно, есть она вообще или нет.
            var data = surface.navMeshData;

            if (data == null)
            {
                Debug.LogError("[IsoRPG] Навигация НЕ построилась: сетки нет. " +
                               "Печём по коллайдерам — значит либо их нет, " +
                               "либо они не попали в сбор.");
                return;
            }

            var box = data.sourceBounds;

            Debug.Log("[IsoRPG] Навигация перепечена по коллайдерам. Охват " +
                      box.size.x.ToString("0") + " x " + box.size.z.ToString("0") +
                      " м, центр " + box.center + ".");

            Persist(surface, ground.name);
        }

        /// <summary>
        /// Сохранить готовую сетку файлом рядом со сценой.
        ///
        /// <b>Без этого шага работы не видно только в сборке.</b> BuildNavMesh
        /// строит сетку в памяти редактора: щуп её видит, агенты в режиме Play
        /// ходят, всё выглядит исправным. Но в билд попадает то, что лежит на
        /// диске, — и там не оказывается ничего. В игре это 121 строчка
        /// «Failed to create agent because there is no valid NavMesh» и
        /// столько же существ, стоящих столбом.
        ///
        /// Шаг был написан один раз, внутри строителя песочницы, и когда мир
        /// переехал на арену, с ним не поехал. Поэтому он теперь здесь: у
        /// выпечки восемь вызовов, и каждый терял сетку одинаково.
        /// </summary>
        private static void Persist(NavMeshSurface surface, string groundName)
        {
            var scene = surface.gameObject.scene;

            // Несохранённая сцена не имеет пути, и класть файл некуда.
            // Выпечка при этом молчит — поэтому говорим мы.
            if (string.IsNullOrEmpty(scene.path))
            {
                Debug.LogWarning("[IsoRPG] Сетка построена, но сцена не сохранена — " +
                                 "файл класть некуда. В сборке навигации не будет.");
                return;
            }

            // Уже лежит файлом (например, сцену пекли повторно) — тогда
            // достаточно записать изменения, а не создавать заново.
            if (AssetDatabase.Contains(surface.navMeshData))
            {
                EditorUtility.SetDirty(surface.navMeshData);
                AssetDatabase.SaveAssets();
                Debug.Log("[IsoRPG] Навигационная сетка обновлена в файле " +
                          AssetDatabase.GetAssetPath(surface.navMeshData));
                return;
            }

            string parent = System.IO.Path.GetDirectoryName(scene.path).Replace('\\', '/');
            string folderName = System.IO.Path.GetFileNameWithoutExtension(scene.path);
            string folder = parent + "/" + folderName;

            if (!AssetDatabase.IsValidFolder(folder))
                AssetDatabase.CreateFolder(parent, folderName);

            string path = folder + "/NavMesh-" + groundName + ".asset";

            // Старый файл сносим до создания нового: CreateAsset поверх
            // существующего оставляет сцену со ссылкой на прежний объект, и
            // обновлённая сетка молча не применяется.
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(surface.navMeshData, path);
            AssetDatabase.SaveAssets();

            EditorUtility.SetDirty(surface);

            Debug.Log("[IsoRPG] Навигационная сетка сохранена файлом: " + path);
        }
    }
}
