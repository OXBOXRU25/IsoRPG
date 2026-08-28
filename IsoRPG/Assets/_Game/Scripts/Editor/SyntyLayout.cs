using UnityEngine;

namespace IsoRPG.EditorTools
{
    /// <summary>
    /// Словарь деталей POLYGON Dungeons: чем строим на новом наборе.
    ///
    /// Отдельно от <see cref="RuinsLayout"/>, а не вместо него: старый набор
    /// держит нынешнюю игру, и пока новая локация не встала, ломать его
    /// нельзя. Символы карты общие — планировки переносятся между наборами
    /// без переписывания.
    ///
    /// ЧТО ЗДЕСЬ ВАЖНО ЗНАТЬ, ПРЕЖДЕ ЧЕМ ПРАВИТЬ
    ///
    /// **Клетка пять метров, а не четыре.** У KayKit была четвёрка; здесь
    /// пол ровно 5×5, стена 5.24 — с нахлёстом на стык, чтобы в углах не
    /// оставалось щели. Числа сняты замером (`Tools → Замерить модуль
    /// Synty`), а не взяты с потолка: «наверное, тоже четыре» — самый
    /// дорогой способ узнать, что нет.
    ///
    /// **Точка отсчёта у деталей в углу, а не в центре.** У стены центр
    /// смещён на −2.47 по X, у пола на (−2.5, +2.5). Поставишь деталь прямо
    /// в центр клетки — вся постройка уедет по диагонали на полклетки, и
    /// заметно это станет клетке на десятой. Поэтому <see cref="PivotShift"/>
    /// применяется ко всему.
    ///
    /// **Углов не существует.** У KayKit угол был отдельной деталью; здесь
    /// стены стыкуются встык и угол образуется их пересечением. Ставить в
    /// угол «угловую деталь» нечего и не надо.
    ///
    /// **Зато есть торцы** (`Culled`) — ими закрывают обрыв стены, иначе
    /// видно изнанку геометрии. И есть потолки: подземелье может стать
    /// закрытым, а не комнатой без крыши.
    /// </summary>
    public static class SyntyLayout
    {
        private const string Root = "Assets/PolygonDungeon/Prefabs";
        private const string Env = Root + "/Environments";
        private const string Props = Root + "/Props";

        /// <summary>Размер клетки в метрах. Снят замером пола: ровно 5×5.</summary>
        public const float Cell = 5f;

        /// <summary>
        /// Поправка на угловой пивот: деталь ставится этим сдвигом, чтобы её
        /// середина пришлась на середину клетки.
        /// </summary>
        public static readonly Vector3 PivotShift = new Vector3(Cell * 0.5f, 0f, -Cell * 0.5f);

        // ------------------------------------------------------------------
        // Стены и проёмы
        // ------------------------------------------------------------------

        /// <summary>
        /// Целые стены. Пятнадцать вариантов — берём первые семь: дальше
        /// идут вариации с трещинами, которые лучше ставить осмысленно, а не
        /// вперемешку.
        /// </summary>
        public static readonly string[] Walls =
        {
            Env + "/Walls/SM_Env_Wall_01.prefab",
            Env + "/Walls/SM_Env_Wall_02.prefab",
            Env + "/Walls/SM_Env_Wall_03.prefab",
            Env + "/Walls/SM_Env_Wall_04.prefab",
            Env + "/Walls/SM_Env_Wall_05.prefab",
            Env + "/Walls/SM_Env_Wall_06.prefab",
            Env + "/Walls/SM_Env_Wall_07.prefab",
        };

        /// <summary>Торец: закрывает обрыв кладки на краю постройки.</summary>
        public static readonly string[] WallEnds =
        {
            Env + "/Walls/SM_Env_Wall_Culled_01.prefab",
            Env + "/Walls/SM_Env_Wall_Culled_02.prefab",
            Env + "/Walls/SM_Env_Wall_Culled_03.prefab",
            Env + "/Walls/SM_Env_Wall_Culled_04.prefab",
        };

        /// <summary>Обрушенная кладка — для разрушенных участков.</summary>
        public static readonly string[] WallsBroken =
        {
            Env + "/Walls/SM_Env_Wall_Broken_Edge_01.prefab",
            Env + "/Walls/SM_Env_Wall_Broken_Edge_02.prefab",
        };

        /// <summary>Проём в стене: через него ходят.</summary>
        public const string Doorway = Env + "/Walls/SM_Env_Wall_DoorFrame_01.prefab";

        /// <summary>Двойной проём — под запись «DD» на карте.</summary>
        public const string DoorwayDouble = Env + "/Walls/SM_Env_Wall_DoorFrame_Double_01.prefab";

        /// <summary>Арка: проём без створок, для галерей.</summary>
        public const string Archway = Env + "/Walls/SM_Env_Wall_Archway_01.prefab";

        /// <summary>Стена с окном — сквозь неё видно.</summary>
        public static readonly string[] Windows =
        {
            Env + "/Walls/SM_Env_Wall_Window_01.prefab",
            Env + "/Walls/SM_Env_Wall_Window_02.prefab",
        };

        /// <summary>Ниша: углубление в стене под статую или сундук.</summary>
        public const string Alcove = Env + "/Walls/SM_Env_Wall_Alcove_Round_01.prefab";

        // ------------------------------------------------------------------
        // Полы и потолки
        // ------------------------------------------------------------------

        /// <summary>
        /// Плиты пола. Двадцать восемь вариантов; берём разнобой из целых —
        /// одна плитка на весь пол читается как кафель, а не как подземелье.
        /// </summary>
        public static readonly string[] Floors =
        {
            Env + "/Floors/SM_Env_Tiles_01.prefab",
            Env + "/Floors/SM_Env_Tiles_02.prefab",
            Env + "/Floors/SM_Env_Tiles_03.prefab",
            Env + "/Floors/SM_Env_Tiles_04.prefab",
            Env + "/Floors/SM_Env_Tiles_05.prefab",
            Env + "/Floors/SM_Env_Tiles_06.prefab",
        };

        /// <summary>Плоский потолок — основной.</summary>
        public static readonly string[] Ceilings =
        {
            Env + "/Walls/SM_Env_Ceiling_Stone_Flat_01.prefab",
            Env + "/Walls/SM_Env_Ceiling_Stone_Flat_02.prefab",
            Env + "/Walls/SM_Env_Ceiling_Stone_Flat_03.prefab",
        };

        /// <summary>Свод — для залов, где потолок виден и должен быть красив.</summary>
        public static readonly string[] CeilingsVaulted =
        {
            Env + "/Walls/SM_Env_Ceiling_Stone_Curved_01.prefab",
            Env + "/Walls/SM_Env_Ceiling_Stone_Curved_02.prefab",
            Env + "/Walls/SM_Env_Ceiling_Stone_Curved_03.prefab",
        };

        // ------------------------------------------------------------------
        // Опоры и обстановка
        // ------------------------------------------------------------------

        public static readonly string[] Pillars =
        {
            Env + "/Pillars/SM_Env_Pillar_Round_01.prefab",
            Env + "/Pillars/SM_Env_Pillar_Round_02.prefab",
            Env + "/Pillars/SM_Env_Pillar_Square_01.prefab",
            Env + "/Pillars/SM_Env_Pillar_Square_02.prefab",
        };

        public static readonly string[] PillarsBroken =
        {
            Env + "/Pillars/SM_Env_Pillar_Broken_01.prefab",
            Env + "/Pillars/SM_Env_Pillar_Broken_02.prefab",
        };

        /// <summary>Настенный факел — главный источник света в подземелье.</summary>
        public const string Torch = Props + "/SM_Prop_Torch_Ornate_01.prefab";

        /// <summary>Чаша с огнём: ставится на пол, освещает шире факела.</summary>
        public const string Brazier = Props + "/SM_Prop_Brazier_01.prefab";

        /// <summary>Знамя на стену: закрывает голую кладку в залах.</summary>
        public static readonly string[] Banners =
        {
            Props + "/SM_Prop_Wall_Banner_01.prefab",
            Props + "/SM_Prop_Wall_Banner_02.prefab",
            Props + "/SM_Prop_Wall_Banner_03.prefab",
        };

        /// <summary>
        /// Что ставится по символу карты. Символы те же, что у старого
        /// набора, — планировка переносится как есть.
        /// </summary>
        public static string PropFor(char c)
        {
            switch (c)
            {
                case 'o': return Env + "/Pillars/SM_Env_Pillar_Round_01.prefab";
                case 'x': return Env + "/Pillars/SM_Env_Pillar_Broken_Pile_01.prefab";
                case 'b': return Props + "/SM_Prop_Barrel_01.prefab";
                case 'k': return Props + "/SM_Prop_Crate_Metal_01.prefab";
                case 'c': return Props + "/SM_Prop_Chest_01.prefab";
                case 'l': return Props + "/SM_Prop_Skeleton_Table_01.prefab";
                case 's': return Props + "/SM_Prop_Shelf_01.prefab";
                case 'h': return Props + "/SM_Prop_Stool_01.prefab";

                // Мебель, занимающая клетку целиком.
                case 'B': return Props + "/SM_Prop_Bed_01.prefab";
                case 'm': return Props + "/SM_Prop_StoneTable_01.prefab";
                case 'C': return Props + "/SM_Prop_StoneChair_01.prefab";
                case 'S': return Props + "/SM_Prop_Crate_Metal_02.prefab";
                case 'K': return Props + "/SM_Prop_Barrel_02.prefab";

                default: return null;
            }
        }

        public static bool IsWallChar(char c) => c == '#' || c == 'W' || c == 'T' || c == 'D';
        public static bool HasFloor(char c) => c != ' ';
        public static bool IsOpen(char c) => c == '.' || c == ',';
    }
}
