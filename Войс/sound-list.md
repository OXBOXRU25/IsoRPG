# Звуки IsoRPG — карта и промты

Генерим в **ElevenLabs → Sound Effects** (звуки) и **Text to Speech** (реплики).
Рабочие настройки SFX: **Prompt influence 65%**, длительность выставлять вручную
(не `Auto`), `Looping` включать только для непрерывных звуков.

Ловушки, проверенные на практике:
- **Отрицания не работают.** `no music`, `no human voice` — модель их не понимает
  и часто добавляет наоборот. Писать только то, что нужно услышать.
- **Слова про длительность в тексте промта исполняются буквально.** Написал
  `1 second` — получил огрызок в одну секунду. Длительность задавать полем.
- **Технические термины записи** (`close-mic`, `field recording`, `reverb tail`)
  модель отыгрывает как «сделай атмосферу», а не как зверя. Не писать.
- Одна генерация SFX даёт **4 варианта** (#1–#4) — забирать все, это готовый
  набор для рандома. Цена зависит от длительности, не от числа запусков.
- В TTS наоборот: цена **по символам**, поэтому реплики генерить по одной
  отдельно (иначе получишь один файл, который надо резать, и интонацию
  перечисления).

Формат в проект: **WAV, моно**. Моно обязательно — иначе не работает 3D-позиция
звука, источник будет звучать «из головы».

---

## Приоритет 1 — шаги (важнее всего)

Игрок слышит их непрерывно. По 4 варианта на поверхность, длительность 2s.

```text
Footsteps walking on grass, soft rustling steps on dry earth
```
```text
Footsteps walking on stone, hard boot steps on rock
```
```text
Footsteps walking on wood, hollow boot steps on wooden planks
```
```text
Footsteps splashing through shallow water, wet steps
```

Позже — те же поверхности бегом (`running footsteps`, длительность 2s).

## Приоритет 2 — меч

Взмах (промах) — 2s. Нужно 3–4 варианта разной резкости, это самый частый звук боя:
```text
A heavy steel sword swinging fast through the air, sharp whoosh of a blade cutting air
```

Попадание по телу — 2s:
```text
A sword blade slashing into flesh, heavy wet cutting impact
```

Попадание по броне — 2s:
```text
A steel sword striking metal armor, sharp ringing clang
```

Парирование — 2s:
```text
Two steel swords clashing and scraping against each other, metallic parry ring
```

Вынуть из ножен — 2s:
```text
A steel sword drawn from a leather scabbard, bright metallic ringing shing
```

Убрать в ножны — 2s:
```text
A sword sliding back into a leather scabbard, soft metallic scrape
```

## Приоритет 3 — герой

Кряхтение при ударе, боль, смерть, прыжок, приземление. Длительность 2s:
```text
A man grunting with effort as he swings a heavy weapon
```
```text
A man grunting in pain from a hit, short sharp gasp
```
```text
A man dying, final pained groan fading
```
```text
A man landing heavily on the ground, boots thud with armor rattle
```

## Приоритет 4 — мобы

На **каждого** зверя нужны пять состояний, иначе он звучит сломанно:
агр/угроза, атака, урон, смерть, и «холостой» звук (idle).

**Волк** — 8s вой, 4s рык (с `Looping`), 3s атака, 3s урон, 4s смерть:
```text
A lone wolf howling in the night, long mournful howl rising then fading, distant forest
```
```text
A wolf growling low and menacing, deep threatening rumble through bared teeth, close and dangerous
```
```text
A wolf snarling and barking as it lunges, sharp vicious snap of jaws, aggressive attack
```
```text
A wolf yelping in pain, sharp high pitched whine of a hurt animal
```
```text
A dying wolf whimpering, final weak growl fading into silence
```

**Кабан** — 4s:
```text
An angry wild boar snorting and squealing, aggressive deep grunts, hooves scraping dirt
```

Кабану не хватает урона и смерти — 3s и 4s:
```text
A wild boar squealing in pain, sharp high pitched shriek of a hurt animal
```
```text
A dying wild boar, final wet gurgling grunt fading into silence
```

**Гриб-исполин** — новый босс, стоит в засаде обычным грибом и оживает в семи
метрах. Своих звуков у набора InfinityPBR нет ни одного файла — проверено по
всей папке, поэтому он и молчит. Гнёзда под него в коде уже готовы: как
только файлы лягут в `Assets/_Game/Audio/Voices`, они заиграют сами.

Пробуждение — **4s**, самый важный из пяти: это тот кадр, ради которого
засада и делалась. Игрок должен услышать, что гриб живой, раньше, чем увидит:
```text
A huge fungal creature tearing itself out of wet soil, thick fibrous ripping,
deep groaning creak of stretching wood, low earthy roar
```

Замах — 3s:
```text
A massive mushroom creature swinging a heavy fleshy limb, thick wet whoosh
ending in a deep hollow thump
```

Урон — 2s:
```text
A fungal creature struck hard, wet spongy squelch bursting into a puff of
spores, short pained groan
```

Смерть — 4s:
```text
A huge fungus monster collapsing, heavy wet mass slumping to the ground, long
hissing release of spores fading away
```

Холостой звук — 4s, `Looping` **выключить**: он играет случайно раз в
несколько секунд, петля тут даст ровный гул:
```text
A large fungal creature breathing slowly, wet gurgling and soft creaking of
stretching stalks
```

**Имена файлов под эти пять** (по ним код и находит звуки):
`mush_wake_1`, `mush_attack_1`, `mush_hurt_1`, `mush_death_1`, `mush_idle_1`.
Одна генерация даёт четыре варианта — забирай все и клади с номерами 1–4,
код выберет случайный и не повторит прошлый.

Кабану и волку под урон и смерть — `boar_hurt_1`, `boar_death_1`,
`wolf_hurt_1`, `wolf_death_1`.

**Хрюканья кабана в проекте ровно одно** (`boar_grunt_1.mp3`). Кабанов на карте
десять, хрюкают они раз в семь секунд — один клип на всех слышен как заевший
сэмпл уже на третьем повторе. Достаточно догенерить ещё три.

## Приоритет 5 — интерфейс

Короткие, 1–2s. Здесь модель работает лучше всего:
подбор предмета, надеть/снять снаряжение, клик кнопки, открыть/закрыть сумку,
монеты при покупке, повышение уровня, отказ действия («нельзя»).

```text
Picking up an item, short soft leather and metal clink
```
```text
Equipping armor, metallic buckle and leather strap
```
```text
Coins dropping into a leather pouch, gold jingle
```
```text
A short magical level up chime, bright ascending sparkle
```
```text
A soft dull thud of a denied action, low negative click
```

## Приоритет 6 — атмосфера

Длинные, с включённым `Looping`, 20s+:
лес днём, лес ночью, лагерь, костёр, ветер в поле, река.

```text
Peaceful forest ambience, birds and leaves rustling in wind
```
```text
Night forest ambience, crickets and distant owl
```
```text
A crackling campfire, burning wood
```

---

## Реплики героя (Text to Speech)

Модель **Eleven Multilingual v2**. Настройки: Stability середина, Similarity
три четверти, Style Exaggeration ноль, Speed чуть медленнее середины.

Теги вроде `[exhales]` работают **только на v3** — на v2 они читаются вслух.

**Голос выбрать один раз и озвучить им всё.** Разные голоса между заходами
слышны сразу. Отвергнуты: `Russ – Deep, Smooth and Articulate` (дикторский),
`Liam - Energetic, Social Media Creator` (молодой блогер). Пробуем `Callum`
(низкий, с хрипотцой, сделан под персонажей), запасные — `Bill`, `Daniel`.

Реплику про нехватку ресурса игрок слышит десятки раз за сессию — нужна
ротация из 3–4 формулировок:

```text
Not enough energy.
```
```text
I need to catch my breath.
```
```text
I'm spent.
```
```text
Too tired for that.
```

## Именование файлов

`hero_step_grass_01..04`, `sword_swing_01..04`, `sword_hit_flesh_01..03`,
`wolf_howl_01..04`, `wolf_attack_01..04`, `ui_pickup_01`, `amb_forest_day`.
Номер в конце обязателен даже для одного варианта — код выбирает по номеру.

---

# Где брать готовые звуки

Генерация подходит для металла, ударов и интерфейса. Живые звери, шаги и голос
почти всегда лучше берутся готовыми записями.

## Лицензии — проверять ДО скачивания

Проект коммерческий, поэтому годится не всё:

- **CC0 / Public Domain** — бери и делай что хочешь, атрибуция не нужна.
  Самый безопасный вариант, искать в первую очередь.
- **CC-BY** — коммерция разрешена, но **обязательно указать автора** в титрах.
- **CC-BY-NC** — только некоммерческое. **Нам не подходит**, даже «пока не
  продаём»: заменять придётся потом, когда файл уже вшит в двадцать сцен.
- **Royalty-free** у профессиональных бандлов — разрешено в играх без выплат.

**Вести `CREDITS.md` с первого же скачанного файла.** Через месяц не вспомнишь,
откуда что взято, а для CC-BY это обязательство. Строка на файл: имя файла,
источник, автор, лицензия.

## Источники, по порядку полезности

1. **Sonniss GDC Game Audio Bundle** — `sonniss.com/gameaudiogdc`
   Ежегодный бесплатный бандл профессиональных библиотек, royalty-free для
   коммерческих игр. Десятки гигабайт: шаги по всем поверхностям, оружие,
   животные, атмосфера. Лучшее качество из бесплатного. Качается один раз,
   лежит локально. Начинать отсюда.

2. **Kenney** — `kenney.nl` (раздел Audio)
   Всё **CC0**, атрибуция не нужна. Интерфейс, удары, RPG-паки. Идеален для
   UI-звуков: подбор, клик, монеты, уровень. Мелкие файлы, скачивается за минуту.

3. **Freesound** — `freesound.org`
   Крупнейшая база полевых записей, лучший источник по животным. Лицензии
   смешанные — **обязательно ставить фильтр на CC0** в левой панели поиска.

4. **OpenGameArt** — `opengameart.org`
   Сделано специально под игры, лицензии указаны явно. Объём меньше, зато
   всё пригодно к использованию.

5. **Pixabay Sound Effects** — `pixabay.com/sound-effects`
   Простая свободная лицензия, атрибуция не требуется. Качество среднее,
   но искать быстро.

6. **Unity Asset Store**, фильтр Free + Audio — `assetstore.unity.com`
   Лицензия сразу под движок, импорт без возни с форматами.

**Не брать: BBC Sound Effects** (`sound-effects.bbcrewind.co.uk`). База
шикарная, 33 000 записей, но лицензия только для личного и образовательного
использования — в коммерческую игру нельзя.

## Про платное

Хороший звуковой пак под RPG в Unity Asset Store стоит 20–40 долларов и
закрывает разом шаги, бой и интерфейс в едином характере. Для игры, где звук
слышен непрерывно, это дешевле недели копания в бесплатном — единство
звучания из разнородных бесплатных источников собирается тяжело.

---

# Какие существа реально есть в проекте

Снято 01.09.2026 обходом `IsoRPG/Assets`. В сцене `_Game/Scenes/Sandbox` стоят
все перечисленные ниже, кабан ещё и в `Arena`, лошадь в `ArenaAuthor`.

**Двадцать с лишним моделей, но звуковых наборов нужно девять** — одна раса
делит один набор. Шесть гоблинов не требуют шести наборов, им хватит одного.

| Набор звуков | Модели | Откуда |
|---|---|---|
| Волк | 3 масти (чёрный, бурый, белый) | Polygonal Wolf |
| Кабан | 1 | Malbers, Forest Pack |
| Кабан-босс | 9 вариантов + порченые | Blink |
| Гоблины | 6 (муж/жен, шаман, вождь, 2 воина) | Synty PolygonDungeon |
| Скелеты | 3 (рыцарь, раб, солдат) | Synty PolygonDungeon |
| Гули | 5 (обычный, босс, гниющий, гротескный, падальщик) | BitGem |
| Призраки | 2 | Synty PolygonDungeon |
| Каменный голем | 1 | Synty PolygonDungeon |
| Демон | 1 | Synty PolygonDungeonRealms |
| Лошадь | 1 | POLYGONHorse + Malbers Horse AnimSet |

Плюс NPC-люди без боевых звуков: дворфы, кочевники, горожане Fantasy Kingdom
(трактирщик, кузнец, торговец, король, маг, шут и прочие).

## Состояния взяты из анимаций набора, а не из головы

Это те звуки, которые иначе не угадаешь — они существуют, потому что автор
сделал под них анимацию:

- **Волк:** `Howl` (вой), `Bite`, `Attack`, `Damage`, `Die`, `Eating`,
  `Breath`, `Idle`, `Jump`. Вой нужен обязательно — анимация под него есть.
- **Кабан (Malbers):** `Attack`, `Death`, `Hit`, `Idle`, `Walk`, `Run`,
  `Trot`, `Jump`, `Fall`, **`Sleep`** (храп), **`Swim`** (плеск).
- **Кабан-босс (Blink):** семь разных атак, **`Buff`** (звук усиления),
  `CirclingLeft/Right`, `Death`, **`Eat`**, получение урона с четырёх сторон,
  `IdleCombat`, `Sit`, `Sleep`, стрейфы. Боссу нужен свой набор, тяжелее
  обычного кабана, и отдельный звук на баф.
- **Гуль:** `attack`, **`block`** (парирование), `die`, `gethit`, `idle`,
  `run`, **`spawn`** (появление — звук вылезания из земли), `walk`.

## Характер по наборам

- **Гоблины** — визгливые, высокие, болтливые; годятся «крики гоблинов» и
  тарабарщина. Один комплект на все шесть моделей.
- **Скелеты** — голоса нет вообще: костяной стук, скрежет костей, лязг ржавого
  железа. Ошибка озвучить их рычанием.
- **Гули** — по рендеру это НЕ зомби-трупоеды, а гротескные мутанты с пастями
  и клыками. Звук звериный: низкое утробное урчание и визг, ближе к троллю,
  чем к мертвецу. Плюс `spawn` и `block`. Боссу — тяжёлый удар кувалдой.
- **Призраки** — бестелесное: шёпот, вой ветра, реверберация. Никаких шагов.
- **Голем** — камень: тяжёлый грохот шагов, скрежет трущегося камня, осыпь.
- **Демон** — низкий инфернальный рык с огнём.
- **Лошадь** — ржание, фырканье, топот копыт (отдельно шаг и галоп).

## Что уже куплено и лежит на диске H

Проверять здесь ДО того, как генерить или искать в библиотеках.

**Есть, закрыто:**
- `H:/3/Saund/Fantasy User Interface Sounds - Lite Edition v2.0.unitypackage`
  — весь интерфейс. Приоритет 5 генерить не нужно, ставить этот пакет.
- `H:/3/Music/` — Fantasy RPG Music Pack Vol. 1, 2, 3 + forestsymphony.
- `H:/2/Music/` — готовые mp3: Ambient 1–10, Action Loop и прочее.

**Нет, надо искать:** шаги, оружие, существа, атмосфера (лес, костёр, вода).

**Существа в запасе, ещё не установлены** (`H:/2/Мобы`, `H:/2/Собака`,
`H:/2/Лошадь`): Mushroom Monster, Poly Art Boars, POLYGON Dog Pack,
POLYGON Horse Pack v2.0. Озвучивать их рано — сперва те, кто в сценах.

## Уже озвучено внутри наборов проекта

Проверять здесь ПЕРВЫМ делом — часть звуков едет вместе с моделями.

**Лошадь озвучена полностью**, `Malbers Animations/Horse AnimSet Pro/6 - Audio/Horse`:
пять вариантов шага копыт (`Horse Step_01..05`), три ржания (`horse-whinny_01..03`,
`Neigh1`), три дыхания (`Horse Breathing`), фырканье (`Horse Nose Sound`),
встряхивание головой (`Horse HeadShake`). Искать и генерить для неё нечего.
