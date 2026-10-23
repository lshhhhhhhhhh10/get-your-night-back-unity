// @role Provides English replacements for game messages

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace NightBackPort
{
    // 原网页词典之外，覆盖原生移植新增的动作/作息提示；未匹配返回 null，交给原词典。
    public static class NightPortEnglish
    {
        public static Dictionary<string, string> Entries()
        {
            return new Dictionary<string, string>
            {
                { "练习门", "Practice door" },
                { "卧室门", "Bedroom door" },
                { "旧木门", "Old wooden door" },
                { "客厅收音机", "Living-room radio" },
                { "走廊玩具鸭", "Hallway toy duck" },
                { "后廊收音机", "Back-hall radio" },
                { "客厅矮柜", "Living-room cabinet" },
                { "书房抽屉", "Study drawer" },
                { "书房书柜", "Study bookcase" },
                { "餐边柜", "Kitchen sideboard" },
                { "储物柜", "Storage cabinet" },
                { "洗衣间抽屉", "Laundry drawer" },
                { "练习铁盒", "Practice tin" },
                { "花瓶", "vase" },
                { "铁盒", "tin" },
                { "叉子", "fork" },
                { "铅笔", "pencils" },
                { "长鼾声", "Long snore" },
                { "洗衣机脱水声", "Washer spin cycle" },
                { "入睡 · 听长鼾声", "SETTLING DOWN · LISTEN FOR SNORES" },
                { "洗衣 · 借空间声掩护", "LAUNDRY · BORROW THE NOISE" },
                { "喝水 · 走廊检查", "WATER BREAK · HALLWAY CHECK" },
                { "熄灯 · 观察暗处路线", "LIGHTS OUT · READ THE DARK ROUTES" },
                { "走到暖光里的门把前。先学基础动作，再自己运用。", "Walk to the door handle in the warm light. Learn the basic actions, then use them yourself." },
                { "先听屋里的动静，再去房间找设备。时间改变作息，等一等不会扣分。", "Listen to the house before looking for your device. Routines change with time; waiting costs no points." },
                { "蹲低慢行。要靠墙和家具挡住身体，空地上仍然会被看见。", "Crouch and move slowly. Walls and furniture must hide your body; open floor still leaves you visible." },
                { "站起来了。脚步更快，也更响。", "Standing up. Your footsteps are faster and louder." },
                { "轻轻落脚。", "A quiet step." },
                { "安全带着设备回到卧室。今晚的时间，拿回来了。", "You brought your device safely back to the bedroom. Your night is yours again." },
                { "洗衣机开始脱水。靠近同一声场时，可以借它掩住落脚；隔墙是否有效，要亲耳判断。", "The washer starts spinning. Nearby, its sound can cover a step. Listen carefully: walls change how well the sound travels." },
                { "洗衣机停了，走廊灯亮起。父母到了喝水的时段，先看清路线再动。", "The washer stops and the hall light comes on. Your parent is due for a drink. Watch the route before moving." },
                { "走廊熄灯，远处更难看清。近距离和无遮掩的位置仍然危险。", "The hall goes dark. Distant movement is harder to see, but close range and exposed positions are still dangerous." },
                { "新一轮作息开始。安静等待不扣分，先听呼吸是否平稳。", "The routine starts again. Quiet waiting costs no points. Listen for steady breathing before moving." },
                { "洗衣声快结束了。父母稍后会开灯喝水，可以先选好遮挡和返程路线。", "The washer will stop soon. Your parent will turn on the light and get a drink. Choose cover and a return route now." },
                { "床板响了……父母翻身，准备起床。还有时间停下或躲藏。", "The bed creaks... Your parent shifts and prepares to get up. There is still time to stop or find cover." },
                { "鼾声停了。父母翻了个身，先别急。", "The snoring stops. Your parent turns over. Hold on." },
                { "鼾声重新响起，房间慢慢安静下来。", "Snoring returns. The room is settling down." },
                { "咔哒。脚步从父母房间出来了。", "Click. Footsteps are leaving your parent's room." },
                { "灯光停在你身上——赶快离开视线！", "The light stops on you. Get out of sight!" },
                { "父母看清了你。留意刚才的声音、照明和遮挡，再试一次。", "Your parent saw you clearly. Consider the sounds, lighting, and cover, then try again." },
                { "脚步开始远去。等它真正离开，再继续。", "The footsteps are moving away. Wait until the way is clear before continuing." },
                { "卧室重新传来鼾声。", "Snoring returns from the bedroom." },
                { "按住 E 轻挠，松手停止；Esc 收手。留意停鼾和缩脚。", "Hold E to tickle gently, release to stop, and Esc to withdraw. Watch for curled feet and a pause in the snoring." },
                { "手搭上门把。按住 E 施力，松手即停；R 换开合方向，Esc 收手。", "Your hand is on the handle. Hold E to apply force, release to stop. R reverses direction; Esc lets go." },
                { "设备拿到了，返回发暖光的卧室。", "You have the device. Return to the bedroom with the warm light." },
                { "靠近门、柜子、家猫或声源道具，再按 E。", "Approach a door, cabinet, the cat, or a sound-making prop, then press E." },
                { "手松得太早，物件滑了下去。", "You let go too soon. The object slipped." },
                { "E 练习托放花瓶", "E: PRACTICE SUPPORTING AND PLACING THE VASE" },
                { "E 练习托底、压盖和归位", "E: PRACTICE SUPPORT, LID CONTROL, AND REPLACEMENT" },
                { "E 轻挠熟睡家长的脚底（会刺激家长）", "E: TICKLE YOUR SLEEPING PARENT'S FEET (THIS MAY WAKE THEM)" },
                { "E 轻抚家猫 · Q 丢玩具球", "E: PET THE CAT · Q: TOSS A TOY" },
                { "带设备返回卧室 · 来电前可找掩护", "BRING THE DEVICE HOME · FIND COVER BEFORE A CALL" },
                { "观察环境与声音，寻找设备并返回卧室", "READ THE HOUSE, FIND YOUR DEVICE, AND RETURN HOME" },
                { "手扶着门，屋里又安静下来。E 继续施力 · R 反向 · Esc 收手", "Your hand rests on the door. The house is quiet again. E: push · R: reverse · Esc: let go" },
                { "门边有人，先松开把手挪一挪。Esc 收手", "Someone is in the door's path. Let go and make room. Esc: release" },
                { "门轴声轻下来了，快到头时收住力。", "The hinge grows quieter. Ease off before the door reaches its stop." },
                { "咯吱咯吱——涩点带着把手轻颤。", "Creak, creak... A sticky patch makes the handle tremble." },
                { "吱……门轴有些发涩。", "Creeeak... The hinge feels sticky." },
                { "轻轻咯吱……门缝缓缓合拢。", "A faint creak... The gap slowly closes." },
                { "轻轻咯吱……门缝缓缓张开。", "A faint creak... The gap slowly opens." },
                { "门轴发出一阵咯吱声。", "The hinge creaks." },
                { "砰——！门猛地撞上门框。", "BANG! The door slams into its stop." },
                { "咚，门在尽头碰了一下。", "Thump. The door taps its stop." },
                { "门开了。停一拍，听听有没有回应。", "The door is open. Pause and listen for a response." },
                { "门轻轻合上了。听听门外的动静。", "The door closes gently. Listen to the other side." },
                { "借着环境声走过木板。", "You cross the board under cover of the ambient sound." },
                { "趁着掩护声走过了松动木板。视线仍然危险。", "The sound covered your step over the loose board. You can still be seen." },
                { "松动木板：指针进入中央亮区时按 Space / ×，轻轻落脚；Esc 退开。", "LOOSE BOARD: press Space / A when the pointer enters the lit center. Esc backs away." },
                { "轻轻落脚……", "Putting your foot down gently..." },
                { "稳稳落下。再听听卧室里有没有变化。", "A steady landing. Listen for any change in the bedroom." },
                { "吱呀——这块木板响了。", "CREEAK! The board made a noise." },
                { "A / D 选弹子；按住 E 顶起，接缝齐平时松手。腰形弹子卡住时保持 E 并按 Q 卸力。", "A / D selects a pin. Hold E to lift; release when the seam lines up. If a spool pin binds, keep E held and press Q to ease tension." },
                { "看锁芯里的接缝。顶到边缘齐平时松手；Esc 随时收手听动静。", "Watch the seams inside the lock. Lift to the edge, then release. Esc lets you stop and listen." },
                { "锁芯转开了。轻轻翻找……", "The lock turns open. Search gently..." },
                { "锁里的金属刮响了。先收力听听屋里。", "Metal scrapes inside the lock. Ease off and listen to the house." },
                { "接缝齐了。现在松开 E / R2。", "The seam is aligned. Release E / R2 now." },
                { "咔哒，这根卡住了。松手，A / D 移向下一根。", "Click. That pin is set. Release, then use A / D to choose another." },
                { "弹回来了。看着接缝，再试一次。", "It sprang back. Watch the seam and try again." },
                { "顶过头了，锁芯在发颤。松手卸力。", "Too high. The lock is shaking. Release to ease off." },
                { "腰形弹子卡在台肩。保持 E / R2 顶住，按 Q / L2 卸开扭力。", "The spool pin is binding on its shoulder. Keep E / R2 held and use Q / L2 to ease tension." },
                { "台肩让开了，继续轻轻顶起。", "The shoulder has cleared. Keep lifting gently." },
                { "共用压片被带动，旁边的弹子滑脱了。", "The shared plate moved and a neighboring pin slipped out." },
                { "弹子全部到位，锁芯正在转开。", "All pins are set. The lock is turning open." },
                { "A / D 选弹子 · E / R2 顶起 · 接缝齐平时松手 · Esc 收手", "A / D: SELECT · E / R2: LIFT · RELEASE AT THE SEAM · ESC: WITHDRAW" },
                { "轻轻翻找……有动静时按 Esc 立即停下。", "Searching gently... Press Esc to stop immediately if you hear movement." },
                { "找到了！屏幕亮了，有来电预兆。停下，按住 E 1.2 秒静音。", "Found it! The screen lights up: a call is coming. Stop and hold E for 1.2 seconds to silence it." },
                { "这里没有设备，换个藏点看看。", "No device here. Try another hiding spot." },
                { "花瓶晃了！对准落点，双手托住。", "The vase wobbles! Align your hands and support it with both hands." },
                { "餐具格里的叉子滑出来了！右手接住，左手裹住止响。", "A fork slips out of the cutlery tray! Catch it with your right hand and muffle it with your left." },
                { "碰倒了笔筒！移手拦住三路铅笔。", "You tipped the pencil cup! Move your hand to intercept all three pencils." },
                { "铁盒滑下来了！右手托底，左手压盖。", "The tin is slipping! Support its base with your right hand and hold its lid with your left." },
                { "三枝铅笔全拦住了，轻轻收回笔筒。", "All three pencils caught. Gently return them to the cup." },
                { "手松得太早，物件又滑了下去。", "You released support too soon. The object slipped again." },
                { "袖口裹住叉子，金属的颤动停了。", "Your sleeve wraps around the fork. The metallic vibration stops." },
                { "盒盖叮了一声，左手轻轻压住它。", "The lid clinks. Hold it gently with your left hand." },
                { "两只手用力差太多，花瓶歪了出去。", "Your hands applied uneven force. The vase tilted out of your grip." },
                { "来不及稳稳放回，物件滑了下去。", "You could not replace it in time. The object slipped." },
                { "A / D 对准叉子，E 托住，再用 Q 裹住止响。", "A / D: align with the fork. E: support it. Then Q: wrap and muffle it." },
                { "A / D 对准铁盒，E 托底，Q 压住盒盖。", "A / D: align with the tin. E: support the base. Q: hold the lid." },
                { "A / D 对准花瓶，Q + E 双手托住。左右用力不均会歪。", "A / D: align with the vase. Q + E: support it with both hands. Uneven force makes it tilt." },
                { "保持 E 托住，再按住 Q，用袖口包住叉子，等它停止颤动。", "Keep E held for support. Hold Q to wrap the fork in your sleeve until it stops vibrating." },
                { "保持 E 托底，Q 轻压盒盖，别让它叮当响。", "Keep E held under the tin. Gently hold Q on the lid to prevent a clatter." },
                { "保持 Q + E 托住，把花瓶扶正。", "Keep Q + E held and steady the vase upright." },
                { "保持 Q + E 支撑，A / D 对准原垫，S 向下轻放。", "Keep Q + E held. Use A / D to align with the original mat, then S to lower gently." },
                { "保持 Q + E 支撑，W 向上抬过桌沿并送回，A / D 对准原垫，再 S 轻放。", "Keep Q + E held. W lifts over the edge and carries it back. A / D aligns with the mat; S lowers it gently." },
                { "继续轻轻翻找……Esc 可以停下。", "Continue searching gently... Esc stops the search." },
                { "来电已静音。带手机回到卧室。", "The call is silenced. Bring the phone back to your bedroom." },
                { "来电响了！停下，按住 E 静音。", "The phone is ringing! Stop and hold E to silence it." },
                { "手机铃声传进了走廊。", "The ringtone carries into the hallway." },
                { "来电挂断了。留意父母的脚步。", "The call ended. Listen for your parent's footsteps." },
                { "呼噜噜……猫被摸舒服了，会安静待一会儿。", "Prrrr... The cat enjoys the petting and will stay quiet for a while." },
                { "玩具球滚过去了，猫的注意力被引开了。", "The toy rolls away and draws the cat's attention." },
                { "前面没有安全落点，转向空地再丢玩具球。", "No safe landing spot ahead. Turn toward clear floor before tossing the toy." },
                { "猫扑上桌沿，尾巴扫到了花瓶！对准落点，双手托住。", "The cat jumps onto the table and its tail hits the vase! Align your hands and support it." },
                { "猫盯上了花瓶，正在找起跳的位置。靠近 E 安抚，或 Q 丢玩具。", "The cat eyes the vase and looks for a place to jump. Approach and press E to pet it, or Q to toss a toy." },
                { "猫发现你了。它会跟随；靠近 E 安抚，Q 丢玩具球引开。", "The cat noticed you and may follow. E pets it up close; Q tosses a toy to distract it." },
                { "猫压低了身子——要扑花瓶了！E 安抚，Q 丢玩具，或退开。", "The cat crouches, ready to jump at the vase! E pets it, Q tosses a toy, or back away." },
                { "鼾声停了，脚缩了回去……现在可以松手或 Esc 收手。", "The snoring stops and the feet curl away... Release now, or press Esc to withdraw." },
                { "脚趾蜷了一下，嘴角忍不住上扬。", "The toes curl and a smile slips out." },
                { "手停在被子边，先听听呼吸。按住 E 轻挠 · Esc 收手", "Your hand rests beside the blanket. Listen to the breathing. Hold E: tickle · Esc: withdraw" },
                { "鼾声突然停了，父母缩起脚。再挠可能会醒，松手或 Esc 收手。", "The snoring suddenly stops and your parent pulls their feet back. More tickling may wake them. Release or press Esc." },
                { "走近 · 找到可以接触的地方", "APPROACH · FIND A PLACE TO MAKE CONTACT" },
                { "抓握 · 先握住门把", "GRASP · TAKE THE HANDLE FIRST" },
                { "推 · 接触以后再用力", "PUSH · APPLY FORCE AFTER CONTACT" },
                { "松力 · 停止推动", "RELEASE FORCE · STOP PUSHING" },
                { "推拉 · 换一个方向", "PUSH AND PULL · CHANGE DIRECTION" },
                { "自己运用 · 留出通道", "USE IT YOURSELF · MAKE A WAY THROUGH" },
                { "托住 · 给物件支撑", "SUPPORT · HOLD THE OBJECT UP" },
                { "轻放 · 保持支撑再移动", "PLACE GENTLY · MOVE WHILE SUPPORTING" },
                { "轻抚 · 观察对方的反应", "PET GENTLY · WATCH THE RESPONSE" },
                { "自己运用 · 换成铁盒", "USE IT YOURSELF · TRY THE TIN" },
                { "自由练习 · 自己选择动作", "FREE PRACTICE · CHOOSE YOUR ACTIONS" },
                { "走到门把前，按一次 E / □ 重新握住，再继续当前动作。", "Return to the handle and press E / X once to grasp it again. Then continue this action." },
                { "WASD / 左摇杆移动，鼠标 / 右摇杆转头。先走到暖光里的门把前。", "WASD / left stick moves. Mouse / right stick looks around. Approach the handle in the warm light." },
                { "按一次 E / □，把手搭上门把。抓握建立接触，还没有开始推门。", "Press E / X once to take the handle. Grasping makes contact; it does not push the door yet." },
                { "短按住 E，或轻压 R2。把门推开一小段，留意门的移动和声音。", "Briefly hold E or press R2 gently. Push the door a little and notice its motion and sound." },
                { "松开 E、鼠标和 R2，观察门停在当前角度。停止用力与离开门把是两件事。", "Release E, the mouse, and R2. The door stops at its current angle. Stopping force is different from letting go of the handle." },
                { "R / L1 换开合方向，再轻轻施力，让门往回关一点。", "R / L1 reverses direction. Apply gentle force again to close the door a little." },
                { "自己把门打开，Esc / ○ 收手后穿过去。末段减轻力度，听听有没有撞框。", "Open the door yourself, release it with Esc / B, and walk through. Ease off near the end and listen for a bump." },
                { "到花瓶前按 E。A / D 移手，Q + E 双手托住；手柄用 L2 + R2。松开支撑会掉落，可重试。", "Approach the vase and press E. A / D moves your hands; Q + E supports it with both hands. On a controller, use L2 + R2. Letting go drops it; you can retry." },
                { "保持 Q + E / 双扳机支撑。W 抬过桌沿并送回，A / D 对准原垫，S 向下轻放。", "Keep Q + E / both triggers held. W lifts over the edge and carries back; A / D aligns with the mat; S lowers gently." },
                { "走近暖光里的猫，按一次 E / □ 轻抚。听呼噜，看它坐下；安抚是动作的结果。", "Approach the cat in the warm light and press E / X once to pet it. Hear the purr and watch it sit. Calming is the result of your action." },
                { "到尽头试试铁盒：E 托底，Q 压盖，再用 WASD 放回原垫。手柄仍用双扳机与左摇杆。", "Try the tin at the end: E supports its base, Q holds the lid, and WASD returns it to its mat. On a controller, use both triggers and the left stick." },
                { "继续开关门、托放物件、摸猫，也可到床尾挠痒。Enter / × 完成练习；挠痒会刺激家长，松手停止。", "Keep practicing doors, supporting and placing objects, or petting the cat. You can also tickle at the foot of the bed. Enter / A finishes practice. Tickling stimulates your parent; release to stop." },
                { "做到了。换个情境，再用同样的动作。", "You did it. Try the same action in a different situation." },
                { "基础动作练习完成。正式夜晚里，先观察，再决定怎么运用。", "Basic action practice complete. During the real nights, observe first and decide how to use what you learned." }
            };
        }

        static readonly Dictionary<string, string> translations = Entries();
        static string Name(string value) { string result; return translations.TryGetValue(value, out result) ? result : value; }

        public static string Translate(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            string found;
            if (translations.TryGetValue(text, out found)) return found;
            Match timestamp = Regex.Match(text, @"^(\d{2}:\d{2}\s{2})(.+)$");
            if (timestamp.Success)
            {
                string message = Translate(timestamp.Groups[2].Value);
                if (message != null) return timestamp.Groups[1].Value + message;
            }
            const string doorSuffix = "  松手即停 · R 反向 · Esc 收手";
            if (text.EndsWith(doorSuffix, StringComparison.Ordinal))
            {
                string first = Translate(text.Substring(0, text.Length - doorSuffix.Length));
                if (first != null) return first + "  RELEASE: STOP · R: REVERSE · ESC: LET GO";
            }
            string[] lureNames = { "客厅收音机", "走廊玩具鸭", "后廊收音机" };
            foreach (string name in lureNames)
            {
                if (text == name + " 已定时，2.5 秒后响起。先离开这里。") return Name(name) + " is set to sound in 2.5 seconds. Move away now.";
                if (text == name + " 响了。留意父母是否听见。") return Name(name) + " sounds. Listen for your parent's response.";
                if (text == "E 定时启动" + name) return "E: SET " + Name(name).ToUpperInvariant();
            }
            string[] doorNames = { "卧室门", "旧木门", "练习门" };
            foreach (string name in doorNames) if (text == "E 握住" + name + " · 再按住 E 施力") return "E: GRASP " + Name(name).ToUpperInvariant() + " · THEN HOLD E TO PUSH";
            string[] spotNames = { "客厅矮柜", "书房抽屉", "书房书柜", "餐边柜", "储物柜", "洗衣间抽屉", "练习铁盒" };
            foreach (string name in spotNames)
            {
                if (text == "E 查看机械锁 · " + name) return "E: EXAMINE LOCK · " + Name(name).ToUpperInvariant();
                if (text == "E 翻找 · " + name) return "E: SEARCH · " + Name(name).ToUpperInvariant();
            }
            string[] objectNames = { "花瓶", "铁盒", "叉子", "铅笔" };
            foreach (string name in objectNames)
            {
                if (text == "稳稳接住" + name + "，轻轻放回原处。") return "You caught the " + Name(name) + " and gently returned it to its place.";
                if (text == name + "掉在了地上。先听听父母的动静。") return "The " + Name(name) + " fell to the floor. Listen for your parent.";
            }
            Match value = Regex.Match(text, @"^停下，按住 E / □ 1\.2 秒静音  (\d+)%$");
            if (value.Success) return "STOP AND HOLD E / X FOR 1.2 SECONDS TO SILENCE  " + value.Groups[1].Value + "%";
            value = Regex.Match(text, @"^A / D 移手，按住 E / R2 拦住铅笔。已拦 (\d+) / 3 枝。$");
            if (value.Success) return "A / D: MOVE HAND · HOLD E / R2: CATCH PENCILS · " + value.Groups[1].Value + " / 3 CAUGHT";
            value = Regex.Match(text, @"^拦住了 (\d+) / 3 枝，其余滚到了地上。$");
            if (value.Success) return "Caught " + value.Groups[1].Value + " / 3 pencils. The others rolled onto the floor.";
            return null;
        }
    }
}
