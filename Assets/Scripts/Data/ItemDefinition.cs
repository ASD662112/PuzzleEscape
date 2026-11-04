using System;
using System.Collections.Generic;
using UnityEngine;

namespace PuzzleEscape.Data
{
    [Serializable]
    public class ItemDefinition
    {
        public string Id;
        public string DisplayName;

        /// <summary>玩法描述：这件东西有什么用。</summary>
        public string Description;

        /// <summary>背景档案：与庄园/疗养院故事相关的只言片语，只做氛围与线索。</summary>
        public string Lore;

        public Color Color;
        public bool Unique = true;

        public ItemDefinition(string id, string displayName, string description, Color color, bool unique = true,
            string lore = null)
        {
            Id = id;
            DisplayName = displayName;
            Description = description;
            Lore = lore;
            Color = color;
            Unique = unique;
        }
    }

    public static class ItemIds
    {
        public const string BrassKey = "brass_key";
        public const string Fuse = "fuse";
        public const string SealSun = "seal_sun";
        public const string SealMoon = "seal_moon";
        public const string SealStar = "seal_star";
        public const string WineBottle = "wine_bottle";
        public const string SilverTeacup = "silver_teacup";
        public const string MusicBox = "music_box";
        public const string OldPhoto = "old_photo";
        public const string BasementKey = "basement_key";
        public const string Lantern = "lantern";
        public const string ElevatorGear = "elevator_gear";
        public const string SealShadow = "seal_shadow";
        public const string SilverCandlestick = "silver_candlestick";
        public const string PocketWatch = "pocket_watch";
        public const string VinylRecord = "vinyl_record";
        public const string OldDiary = "old_diary";
        public const string PorcelainVase = "porcelain_vase";
        public const string BrassCompass = "brass_compass";
        public const string OldCoin = "old_coin";
        public const string Candle = "candle";
        public const string ClothScrap = "cloth_scrap";
        public const string EmptyBottle = "empty_bottle";

        // 普通杂物：让柜子/抽屉的随机掉落有更多变化
        public const string Battery = "battery";
        public const string Bandage = "bandage";
        public const string PillBottle = "pill_bottle";
        public const string Newspaper = "newspaper";
        public const string Matchbox = "matchbox";
        public const string Soap = "soap";

        // 疗养院通行卡与钥匙
        public const string Card2F = "card_2f";
        public const string Card3F = "card_3f";
        public const string Card4F = "card_4f";
        public const string StorageKey = "storage_key";
        public const string SurgeryKey = "surgery_key";
        public const string StaffKey = "staff_key";
        public const string DirectorKey = "director_key";
        public const string MainKey = "main_key";

        // 疗养院主线扩展：病房/病人/职员/库房钥匙与三枚保险丝
        public const string WardKey = "ward_key";
        public const string PatientKey = "patient_key";
        public const string ClerkKey = "clerk_key";
        public const string LockerKey = "locker_key";
        public const string FuseA = "fuse_a";
        public const string FuseB = "fuse_b";
        public const string FuseC = "fuse_c";

        // 备用逃生路线：车库卷帘门 / 东侧消防连廊
        public const string GarageKey = "garage_key";
        public const string GateCrank = "gate_crank";
        public const string FireKey = "fire_key";
        public const string FireAxe = "fire_axe";

        /// <summary>三枚总闸保险丝，任意顺序都可以装进配电箱。</summary>
        public static readonly string[] BreakerFuses = { FuseA, FuseB, FuseC };
    }

    /// <summary>
    /// 原型阶段使用代码内的物品数据库，后续可替换为 ScriptableObject 资源。
    /// </summary>
    public static class ItemDatabase
    {
        private static readonly List<ItemDefinition> Definitions = new List<ItemDefinition>
        {
            new ItemDefinition(ItemIds.BrassKey, "黄铜钥匙", "能打开储藏室抽屉的小钥匙。",
                new Color(0.86f, 0.66f, 0.22f), true,
                "女仆长每晚都用它锁好银器。最后一页值夜记录只写了半句：「抽屉里多了别人的影子。」"),
            new ItemDefinition(ItemIds.Fuse, "保险丝", "配电间电闸缺失的零件。",
                new Color(0.88f, 0.36f, 0.22f), true,
                "停电那晚，维修工在总闸下捡到一堆烧黑的保险丝，只有这一枚是完好的。"),
            new ItemDefinition(ItemIds.SealSun, "日之徽记", "封印大门三枚徽记之一，呈琥珀色。",
                new Color(1f, 0.72f, 0.25f), true,
                "庄园的门要三枚徽记同时归位才能打开。日之徽记一直挂在餐厅壁炉上方，谁也不许碰。"),
            new ItemDefinition(ItemIds.SealMoon, "月之徽记", "封印大门三枚徽记之一，呈月蓝色。",
                new Color(0.42f, 0.7f, 1f), true,
                "旧照片里，庄园主的女儿把它缝在礼服内衬，说这样夜里就不会做噩梦。"),
            new ItemDefinition(ItemIds.SealStar, "星之徽记", "封印大门三枚徽记之一，呈星紫色。",
                new Color(0.75f, 0.5f, 1f), true,
                "第三枚徽记在八音盒的暗格里躺了几十年，直到有人重新拧紧了发条。"),
            new ItemDefinition(ItemIds.WineBottle, "陈年红酒", "餐厅长桌上留下的收藏品，已经落满灰尘。",
                new Color(0.5f, 0.08f, 0.12f), true,
                "瓶塞上印着 1937。那场告别宴会只开了一半，客人再也没有回来取走剩下的酒。"),
            new ItemDefinition(ItemIds.SilverTeacup, "银茶杯", "会客厅茶桌上的银质茶杯，杯沿刻着庄园纹章。",
                new Color(0.78f, 0.8f, 0.84f), true,
                "杯底残留着一圈褐色茶渍，像有人喝到一半忽然被叫走，再没回来。"),
            new ItemDefinition(ItemIds.MusicBox, "八音盒", "音乐室钢琴上的八音盒，仍能奏出一段残缺的旋律。",
                new Color(0.78f, 0.58f, 0.24f), true,
                "曲子总在同一个音上停住——庄园主的女儿失踪那天，发条正好卡在这里。"),
            new ItemDefinition(ItemIds.OldPhoto, "旧照片", "客房里一张泛黄的合影，记录着庄园昔日的住客。",
                new Color(0.64f, 0.5f, 0.32f), true,
                "照片背后写着十一个人的名字，可画面里站了十二个人。多出来的那个没有脸。"),
            new ItemDefinition(ItemIds.BasementKey, "地下室钥匙", "一把锈迹斑斑的旧钥匙，能打开锅炉房的地窖铁门。",
                new Color(0.58f, 0.4f, 0.2f), true,
                "锅炉工常年把它挂在脖子上，最后却被人发现锁在地窖门的内侧。"),
            new ItemDefinition(ItemIds.Lantern, "手电筒", "按 E 开关的旧式手电筒，是探索黑暗地下室的关键。",
                new Color(0.78f, 0.82f, 0.72f), true,
                "守夜人的手电。灯罩上缠着一圈歪斜的胶布，像是有人在黑暗里匆忙缠上去的。"),
            new ItemDefinition(ItemIds.ElevatorGear, "电梯齿轮", "电梯传动箱缺失的铜制齿轮，沉甸甸的。",
                new Color(0.76f, 0.52f, 0.2f), true,
                "检修工在日志里写：「齿轮换好之前，别让任何人独自乘电梯上四层。」"),
            new ItemDefinition(ItemIds.SealShadow, "影之徽记", "封印大门的第四枚徽记，呈暗紫色，藏在庄园地下。",
                new Color(0.48f, 0.28f, 0.72f), true,
                "三枚徽记封的是门，第四枚封的是门后的东西。传说它被埋在地窖最深处。"),
            new ItemDefinition(ItemIds.SilverCandlestick, "银烛台", "地下储藏室里翻出的银烛台，烛泪已经凝成琥珀色。",
                new Color(0.8f, 0.82f, 0.86f), true,
                "烛泪里嵌着半枚指纹，比烛台本身更让人不安。"),
            new ItemDefinition(ItemIds.PocketWatch, "黄铜怀表", "表针早已停摆，表壳内侧刻着庄园的旧纹章。",
                new Color(0.78f, 0.62f, 0.28f), true,
                "三枚指针都停在 3:17——正是疗养院全楼断电的那一分钟。"),
            new ItemDefinition(ItemIds.VinylRecord, "黑胶唱片", "一张边缘破损的黑胶唱片，标签上写着某场旧宴会。",
                new Color(0.18f, 0.18f, 0.2f), true,
                "标签上的宴会日期被人用刀片刮掉了，只剩半行乐队签名。"),
            new ItemDefinition(ItemIds.OldDiary, "旧日记", "纸张发脆的日记，记录着庄园仆人在地下走廊里的见闻。",
                new Color(0.62f, 0.5f, 0.34f), true,
                "「墙里有人在走。」这句话被反复写了十几遍，字迹一次比一次用力。"),
            new ItemDefinition(ItemIds.PorcelainVase, "青花瓷瓶", "缺了一角的青花瓷瓶，仍然看得出昔日的光泽。",
                new Color(0.55f, 0.68f, 0.8f), true,
                "缺口很新。瓶身内部的裂纹里，卡着一小片深色的布。"),
            new ItemDefinition(ItemIds.BrassCompass, "黄铜罗盘", "指针不断晃动，似乎对地下的某些地方格外敏感。",
                new Color(0.7f, 0.58f, 0.26f), true,
                "无论怎么转，指针最后都会垂向地下走廊的方向，像被什么牵着。"),
            new ItemDefinition(ItemIds.OldCoin, "旧硬币", "面值早已无法辨认的银币，边缘磨得发亮。",
                new Color(0.66f, 0.64f, 0.56f), false,
                "疗养院食堂的代币，背面刻着「凭此换取一份热汤」。"),
            new ItemDefinition(ItemIds.Candle, "半截蜡烛", "选中后可照亮身边 5 米，最多燃烧 60 秒；切换走会暂停燃烧。",
                new Color(0.86f, 0.8f, 0.62f), false,
                "停电的夜里，病房里的人靠这样的半截蜡烛熬到天亮。有些人没熬过去。"),
            new ItemDefinition(ItemIds.ClothScrap, "碎布片", "从旧窗帘上撕下来的布片，沾着灰尘。",
                new Color(0.42f, 0.34f, 0.3f), false,
                "边缘是整齐的撕口，更像是有人刻意扯下来当作标记。"),
            new ItemDefinition(ItemIds.EmptyBottle, "空酒瓶", "瓶底还残留着一点酒渍的旧玻璃瓶。",
                new Color(0.3f, 0.42f, 0.32f), false,
                "车库守夜人靠它打发长夜，瓶身贴着一张手写便签：「别往西边走。」"),
            new ItemDefinition(ItemIds.Battery, "旧电池", "外壳有些鼓包的旧电池，早就不通电了。",
                new Color(0.35f, 0.3f, 0.18f), false,
                "手电筒的备用电池，被谁一枚枚换下来，堆在抽屉最里侧。"),
            new ItemDefinition(ItemIds.Bandage, "绷带", "卷得整整齐齐的旧绷带，边缘已经发黄。",
                new Color(0.86f, 0.84f, 0.78f), false,
                "绷带上没有血迹，却有一股连消毒水都盖不住的霉味。"),
            new ItemDefinition(ItemIds.PillBottle, "药瓶", "标签被撕掉一半的药瓶，里面只剩几粒白色药片。",
                new Color(0.55f, 0.7f, 0.62f), false,
                "撕掉的那半张标签上还能认出两个字：「镇静」。"),
            new ItemDefinition(ItemIds.Newspaper, "旧报纸", "日期已经模糊的旧报纸，头版是一则失踪启事。",
                new Color(0.72f, 0.68f, 0.58f), false,
                "启事里失踪的七个人，照片与庄园那张合影上的面孔一模一样。"),
            new ItemDefinition(ItemIds.Matchbox, "火柴盒", "还剩几根火柴的旧纸盒，盒面印着模糊的旅店标志。",
                new Color(0.62f, 0.3f, 0.22f), false,
                "盒面的旅店标志和疗养院门厅的徽章如出一辙，像出自同一个人之手。"),
            new ItemDefinition(ItemIds.Soap, "肥皂", "用得只剩薄片的一块肥皂，还带着淡淡的味道。",
                new Color(0.8f, 0.82f, 0.75f), false,
                "洗衣房的肥皂总在半夜少一块。值夜的人从不敢去问是谁拿的。"),
            new ItemDefinition(ItemIds.Card2F, "二层通行卡", "印着「2F 病区」的磁卡，可打开二层病区门。",
                new Color(0.55f, 0.78f, 0.9f), true,
                "背面印着护士的工号，边缘被反复刷卡磨得发白。"),
            new ItemDefinition(ItemIds.Card3F, "三层通行卡", "印着「3F 手术区」的磁卡，可打开三层手术区门。",
                new Color(0.45f, 0.85f, 0.75f), true,
                "卡面下方有一道擦不掉的暗色指印，正好落在照片里手术室门的位置。"),
            new ItemDefinition(ItemIds.Card4F, "四层通行卡", "印着「4F 行政区」的磁卡，可打开四层行政区门。",
                new Color(0.78f, 0.72f, 0.95f), true,
                "裂缝从持卡人照片的颈部横穿过去，像有人故意折断过一次。"),
            new ItemDefinition(ItemIds.StorageKey, "药库钥匙", "药房柜子的钥匙，钥匙柄上缠着褪色的胶布。",
                new Color(0.82f, 0.72f, 0.38f), true,
                "胶布上用铅笔写着「只借一次」，字迹被人反复描粗。"),
            new ItemDefinition(ItemIds.SurgeryKey, "手术区钥匙", "手术室器械柜的钥匙，冰冷得不太正常。",
                new Color(0.72f, 0.78f, 0.84f), true,
                "握久了指尖会发麻，比这层楼里任何金属都冷。"),
            new ItemDefinition(ItemIds.StaffKey, "值班钥匙", "四层值班室的门钥匙，钥匙环上还挂着交接班牌。",
                new Color(0.66f, 0.74f, 0.8f), true,
                "交接班牌上最后一个名字被人用力划掉了，划痕深得几乎穿透铁皮。"),
            new ItemDefinition(ItemIds.DirectorKey, "院长室钥匙", "刻着「行政」二字的旧钥匙，齿口磨损严重。",
                new Color(0.9f, 0.78f, 0.42f), true,
                "齿口磨损得厉害，说明它被开过太多次——远超过一扇办公室的门所需。"),
            new ItemDefinition(ItemIds.MainKey, "主门钥匙", "挂在院长室铁环上的长柄钥匙，能打开一层正门。",
                new Color(0.95f, 0.85f, 0.5f), true,
                "逃生最近的钥匙，却被留在离正门最远的一间屋子里。"),
            new ItemDefinition(ItemIds.WardKey, "病房钥匙", "一层病区通用的老钥匙，钥匙柄上缠着白色胶布。",
                new Color(0.8f, 0.8f, 0.74f), true,
                "胶布上写着「1F 全线」。胶布底下，还压着另一行更旧的字。"),
            new ItemDefinition(ItemIds.PatientKey, "病人物品柜钥匙", "刻着床位号的小钥匙，能打开四层病房的储物柜。",
                new Color(0.74f, 0.82f, 0.86f), true,
                "每把这样的钥匙对应一个床位，而四层的床位登记簿缺了整整一页。"),
            new ItemDefinition(ItemIds.ClerkKey, "职员钥匙", "地下值班室抽屉里的钥匙，能打开地下档案室。",
                new Color(0.86f, 0.74f, 0.44f), true,
                "档案室的钥匙本该有三把，行政记录里却只登记了一把。"),
            new ItemDefinition(ItemIds.LockerKey, "库房钥匙", "洗衣房抽屉里的钥匙，齿口很浅，只能开地下库房。",
                new Color(0.7f, 0.74f, 0.8f), true,
                "齿口很浅，只能开库房。而库房里堆着的东西，账本上一件都没写。"),
            new ItemDefinition(ItemIds.FuseA, "保险丝 · 甲", "总闸缺的第一枚保险丝，铜帽还亮着。",
                new Color(0.9f, 0.4f, 0.25f), true,
                "铜帽还亮着，像是刚被人从别的配电箱里拆下来。"),
            new ItemDefinition(ItemIds.FuseB, "保险丝 · 乙", "总闸缺的第二枚保险丝，玻璃管里有一道裂纹。",
                new Color(0.92f, 0.52f, 0.26f), true,
                "玻璃管里有一道裂纹，却干净得不沾一丝灰——它从没被装上过。"),
            new ItemDefinition(ItemIds.FuseC, "保险丝 · 丙", "总闸缺的第三枚保险丝，用油纸包着。",
                new Color(0.94f, 0.64f, 0.3f), true,
                "油纸包得很仔细，像有人打算把它留到「那一天」再用。"),
            new ItemDefinition(ItemIds.GarageKey, "车库钥匙", "挂着木牌的粗钥匙，能打开负一层车库门。",
                new Color(0.82f, 0.7f, 0.4f), true,
                "木牌上「负一层·车库」被人添了一笔，改成了「负一层·别进去」。"),
            new ItemDefinition(ItemIds.GateCrank, "卷帘门曲柄", "一根 L 形手摇曲柄，用来摇起车库卷帘门。",
                new Color(0.66f, 0.7f, 0.76f), true,
                "曲柄上缠着机油和布条，说明最后一次使用非常匆忙。"),
            new ItemDefinition(ItemIds.FireKey, "消防连廊钥匙", "红色钥匙牌上写着「4F 东侧消防连廊」。",
                new Color(0.9f, 0.36f, 0.3f), true,
                "钥匙牌背面贴着消防演习的日期——同一天，疗养院被永久关闭。"),
            new ItemDefinition(ItemIds.FireAxe, "消防斧", "斧刃有些卷口，但仍能撬开锈死的盖板。",
                new Color(0.78f, 0.32f, 0.24f), true,
                "斧柄上刻着「最后一次检查：3:17」，和那块怀表停在同一分钟。")
        };

        public static IReadOnlyList<ItemDefinition> All => Definitions;

        public static ItemDefinition Get(string itemId)
        {
            if (string.IsNullOrEmpty(itemId))
            {
                return null;
            }

            foreach (ItemDefinition definition in Definitions)
            {
                if (definition.Id == itemId)
                {
                    return definition;
                }
            }

            return null;
        }
    }
}
