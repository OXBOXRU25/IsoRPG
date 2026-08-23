using UnityEngine;

namespace IsoRPG.EditorTools
{
    /// <summary>
    /// Планировка руин, записанная картой.
    ///
    /// Карта нарисована текстом, потому что её надо ЧИТАТЬ и править глазом.
    /// Список координат не рассказывает, где зал, а где двор; карта
    /// рассказывает с первого взгляда, и меняется без единой строчки кода.
    ///
    /// СЛОВАРЬ СИМВОЛОВ
    ///
    ///   пробел  снаружи, ничего
    ///   #       стена (углы и трещины подставляются сами)
    ///   W       стена с открытым окном — сквозь неё видно
    ///   T       стена с факелом
    ///   D       проём, через него ходят
    ///   .       каменный пол
    ///   ,       земляной пол (двор, дорожки)
    ///   o       колонна        x  обломки
    ///   b       бочка          k  ящики
    ///   c       сундук         l  стол
    ///   s       полка          h  табурет
    ///
    /// Правила, которые стоит держать в голове, рисуя:
    ///   • у каждой комнаты должен быть хотя бы один проём, иначе туда не
    ///     войти — навигация просто не найдёт пути;
    ///   • разрывы в стенах важнее самих стен: они дают укрытия от стрел и
    ///     пути обхода, то есть работают на бой, а не только на вид;
    ///   • земляной пол по кромке смягчает переход от камня к траве, без
    ///     него постройка обрывается ножом.
    /// </summary>
    public static class RuinsLayout
    {
        /// <summary>
        /// План: главный зал с колоннами, малая комната-хранилище справа,
        /// разрушенная пристройка сверху, дорожки во двор.
        /// </summary>
        public static readonly string[] Map =
        {
            "     ,,,,,,,,,,                 ",
            "    ,,#WTDDTW#,,                ",
            "   ,,#lC.....Cs#,,,,,,,,,,,,,,,,",
            "   ,#T..o...o..T#,,,,,,#########",
            "   ,W.m.......m.W,,,,,,#T.....T#",
            "  ,,D.....l.....D,,,,,,,D......#",
            "   ,W.h.......h.W,,,,,,#..x....#",
            "   ,#T..o...o..T#,,,,,,#..o.o..#",
            "   ,,#skS.....bk#,,,,,,#T.x...T#",
            "    ,,#W#DD#W#,,,,,,,,,#########",
            "     ,,,#..#,,,,,,,,,,,,,,,,,,,,",
            "      ,,#..#####,,,             ",
            "      ,,#B..S..W#,,             ",
            "     ,,,D..c.mC.#,,             ",
            "      ,,#h.l..K.#,              ",
            "      ,,#WW#DD#W#,              ",
            "       ,,,,,,,,,,,              ",
        };

        private const string WallModel = "wall";
        private const string WallCracked = "wall_cracked";
        private const string WallBroken = "wall_broken";
        private const string WallCorner = "wall_corner";
        private const string WallWindow = "wall_archedwindow_open";
        private const string DoorwayModel = "wall_doorway";
        private const string TorchWall = "torch_mounted";
        private const string PillarModel = "pillar";
        private const string FloorStone = "floor_tile_large";
        private const string FloorDirt = "floor_dirt_large";

        /// <summary>Есть ли на клетке пол.</summary>
        /// <summary>Размер клетки плана в метрах. Совпадает с плиткой набора.</summary>
        public const float Cell = 4f;

        /// <summary>
        /// Где на самом деле находится середина главного зала.
        ///
        /// Считается из карты, а не пишется числом: карта уже однажды выросла
        /// вправо, и все координаты, записанные руками, разом стали врать —
        /// игрок с торговцем оказались внутри восточной стены.
        /// </summary>
        public static Vector3 HallCentre => CellToWorld(10, 5);

        /// <summary>Середина склепа: там стоит владыка.</summary>
        public static Vector3 CryptCentre => CellToWorld(26, 6);

        public static Vector3 CellToWorld(int col, int row)
        {
            int rows = Map.Length;
            int cols = 0;
            foreach (var line in Map) cols = Mathf.Max(cols, line.Length);

            float offsetX = -(cols - 1) * Cell * 0.5f;
            float offsetZ = (rows - 1) * Cell * 0.5f;

            return new Vector3(offsetX + col * Cell, 0f, offsetZ - row * Cell);
        }

        public static bool HasFloor(char c) => c != ' ';

        /// <summary>
        /// Каменные плиты руин: целые, треснувшие, заросшие, с осыпью.
        ///
        /// Одна плитка на весь пол читается как кафель, а не как развалины.
        /// Разнобой стоит ничего — модели уже лежат в наборе — и делает
        /// половину работы по превращению «постройки» в «руины».
        /// </summary>
        /// <summary>
        /// Только ПОЛНОРАЗМЕРНЫЕ плиты.
        ///
        /// Мелкие варианты набора («small») занимают четверть клетки, и
        /// подмешанные в общий пол оставляли вокруг себя дыры в земле. Ошибка
        /// обидная тем, что сами плитки выглядели хорошо — а рядом с ними
        /// зияла пустота.
        ///
        /// Разнообразие даёт не подмена плиты, а накладка поверх неё.
        /// </summary>
        private static readonly string[] StoneFloors =
        {
            FloorStone, FloorStone, FloorStone, FloorStone,
            "floor_tile_large_rocks",
        };

        private static readonly string[] DirtFloors =
        {
            FloorDirt, FloorDirt, FloorDirt,
            "floor_dirt_large_rocky",
        };

        /// <summary>
        /// Мелкие плиты, которые кладутся СВЕРХУ на целую: трещины, сорняки,
        /// осыпь. Дыр не оставляют — под ними полноценный пол.
        /// </summary>
        private static readonly string[] FloorPatches =
        {
            "floor_tile_small_weeds_A",
            "floor_tile_small_weeds_B",
            "floor_tile_small_broken_A",
            "floor_tile_small_broken_B",
            "floor_tile_small_decorated",
        };

        /// <summary>Накладка для клетки. Пусто — плита остаётся чистой.</summary>
        public static string PatchFor(int col, int row)
        {
            int hash = (col * 40503) ^ (row * 12289);
            if (hash < 0) hash = -hash;

            // Примерно каждая пятая клетка: сплошной ковёр из трещин читается
            // как узор, а не как разрушение.
            if (hash % 5 != 0) return null;

            return FloorPatches[(hash / 5) % FloorPatches.Length];
        }

        /// <summary>
        /// Плитка под клеткой. Вариант выбирается по КООРДИНАТАМ, а не
        /// случайно в момент постройки: иначе одна и та же карта каждый раз
        /// выглядит иначе, и сравнить два прогона невозможно.
        /// </summary>
        public static string FloorFor(char c, int col, int row)
        {
            var pool = (c == ',' || c == 'x') ? DirtFloors : StoneFloors;

            // Простая перемешка координат: соседние клетки получают разные
            // варианты, а вся карта остаётся одинаковой от сборки к сборке.
            int hash = (col * 73856093) ^ (row * 19349663);
            if (hash < 0) hash = -hash;

            return pool[hash % pool.Length];
        }

        public static string FloorFor(char c) => FloorFor(c, 0, 0);

        /// <summary>Стена, если клетка — стена. Иначе пусто.</summary>
        public static string WallFor(char c)
        {
            switch (c)
            {
                case '#': return WallModel;
                case 'W': return WallWindow;
                case 'T': return WallModel;      // факел ставится сверху
                case 'D': return DoorwayModel;
                default:  return null;
            }
        }

        public static bool IsWallChar(char c) =>
            c == '#' || c == 'W' || c == 'T' || c == 'D';

        /// <summary>Проходимая клетка — по ней ходят и на ней стоит мебель.</summary>
        public static bool IsOpen(char c) =>
            c == '.' || c == ',' || c == 'o' || c == 'x' ||
            c == 'b' || c == 'k' || c == 'c' || c == 'l' ||
            c == 's' || c == 'h';

        public static string PropFor(char c)
        {
            switch (c)
            {
                case 'o': return PillarModel;
                case 'x': return "rubble_half";
                case 'b': return "barrel_large";
                case 'k': return "crates_stacked";
                case 'c': return "chest";
                case 'l': return "table_long_broken";
                case 's': return "shelf_small_candles";
                case 'h': return "stool";

                // Жилая утварь малой комнаты. Кровать и накрытый стол делают
                // из склада жильё: видно, что тут не только хранили, но и
                // ночевали.
                case 'B': return "bed_decorated";
                case 'm': return "table_medium_tablecloth";
                case 'C': return "chair";
                case 'S': return "shelves";
                case 'K': return "keg_decorated";
                default:  return null;
            }
        }

        /// <summary>Крупный ли предмет — такой перекрывает выстрел.</summary>
        public static bool IsSolidProp(char c) => c == 'o' || c == 'k';

        public static bool HasTorch(char c) => c == 'T';
        public static string TorchModel => TorchWall;
        public static string CornerModel => WallCorner;
        public static string CrackedModel => WallCracked;
        public static string BrokenModel => WallBroken;

        /// <summary>
        /// Каждая пятая стена потрескалась. Не случайность ради случайности:
        /// одинаковые секции по всему периметру читаются как обои, а нам
        /// нужны руины, простоявшие столетие.
        /// </summary>
        public static bool ShouldCrack(int x, int z) => ((x * 5 + z * 3) % 5) == 0;

        /// <summary>Каждая девятая — обрушена совсем.</summary>
        public static bool ShouldBreak(int x, int z) => ((x * 7 + z * 11) % 9) == 0;
    }
}
