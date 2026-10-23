// @role Guides the eleven-step basic-action practice
// @calls NightGame_Interactions

using UnityEngine;

namespace NightBackPort
{
    public partial class NightGame
    {
        public int tutorialStep;
        public float tutorialHold, tutorialLastDoor, tutorialFeedbackLeft;
        public string tutorialFeedback;
        public const int TutorialStepCount = 11;

        void ResetTutorial()
        {
            tutorialStep = 0; tutorialHold = 0; tutorialLastDoor = 0; tutorialFeedbackLeft = 0;
            tutorialFeedback = "";
        }

        public string TutorialTitle()
        {
            string[] titles = {
                "走近 · 找到可以接触的地方", "抓握 · 先握住门把", "推 · 接触以后再用力",
                "松力 · 停止推动", "推拉 · 换一个方向", "自己运用 · 留出通道",
                "托住 · 给物件支撑", "轻放 · 保持支撑再移动", "轻抚 · 观察对方的反应",
                "自己运用 · 换成铁盒", "自由练习 · 自己选择动作"
            };
            return titles[Mathf.Clamp(tutorialStep, 0, titles.Length - 1)];
        }

        public string TutorialHint()
        {
            if (levelIndex != 3) return "";
            if (tutorialStep >= 1 && tutorialStep <= 4 && (mode == null || mode.type != "door"))
                return "走到门把前，按一次 E / □ 重新握住，再继续当前动作。";
            string[] hints = {
                "WASD / 左摇杆移动，鼠标 / 右摇杆转头。先走到暖光里的门把前。",
                "按一次 E / □，把手搭上门把。抓握建立接触，还没有开始推门。",
                "短按住 E，或轻压 R2。把门推开一小段，留意门的移动和声音。",
                "松开 E、鼠标和 R2，观察门停在当前角度。停止用力与离开门把是两件事。",
                "R / L1 换开合方向，再轻轻施力，让门往回关一点。",
                "自己把门打开，Esc / ○ 收手后穿过去。末段减轻力度，听听有没有撞框。",
                "到花瓶前按 E。A / D 移手，Q + E 双手托住；手柄用 L2 + R2。松开支撑会掉落，可重试。",
                "保持 Q + E / 双扳机支撑。W 抬过桌沿并送回，A / D 对准原垫，S 向下轻放。",
                "走近暖光里的猫，按一次 E / □ 轻抚。听呼噜，看它坐下；安抚是动作的结果。",
                "到尽头试试铁盒：E 托底，Q 压盖，再用 WASD 放回原垫。手柄仍用双扳机与左摇杆。",
                "继续开关门、托放物件、摸猫，也可到床尾挠痒。Enter / × 完成练习；挠痒会刺激家长，松手停止。"
            };
            if (mode != null && (mode.type == "catch" || mode.type == "tickle")) return mode.hint;
            return hints[Mathf.Clamp(tutorialStep, 0, hints.Length - 1)];
        }

        public Vector3 TutorialTarget()
        {
            if (tutorialStep == 0) return new Vector3(3, 0, 13.05f);
            if (tutorialStep >= 1 && tutorialStep <= 4) return new Vector3(3, 0, player.z < 12 ? 10.95f : 13.05f);
            if (tutorialStep == 5) return new Vector3(3, 0, 10.9f);
            if (tutorialStep == 6 || tutorialStep == 7) return new Vector3(2.25f, 0, 8.3f);
            if (tutorialStep == 8) return new Vector3(3, 0, 6.5f);
            if (tutorialStep == 9) return new Vector3(3, 0, 2.2f);
            return player;
        }

        string TutorialStationNear()
        {
            if (levelIndex != 3 || mode != null) return null;
            if (tutorialStep >= 6 && FlatDistance(player, new Vector3(2.25f, 0, 8.3f)) < 1.05f) return "vase";
            if (tutorialStep >= 9 && FlatDistance(player, new Vector3(3, 0, 2.2f)) < 1.05f) return "storage-search";
            return null;
        }

        void TickTutorial(float dt, NightInput input)
        {
            if (levelIndex != 3 || !active || status != "playing" || doors.Count == 0) return;
            tutorialFeedbackLeft = Mathf.Max(0, tutorialFeedbackLeft - dt);
            NightDoor door = doors[0];
            bool pushed = input.actionHeld || input.force > .035f || input.rightForce > .035f;
            bool released = !pushed && door.speed <= .001f;
            bool done = false;
            switch (tutorialStep)
            {
                case 0: done = DoorNear() == door || (mode != null && mode.type == "door"); break;
                case 1: done = mode != null && mode.type == "door"; break;
                case 2: done = door.progress >= .16f; break;
                case 3: tutorialHold = released ? tutorialHold + dt : 0; done = tutorialHold >= .6f; break;
                case 4: done = door.progress < tutorialLastDoor - .0001f && mode != null && mode.type == "door" && door.direction == -1; break;
                case 5: done = mode == null && player.z <= 11.02f; break;
                case 6: done = (mode != null && mode.id == "vase" && rescue != null && (rescue.stage == "steady" || rescue.stage == "lower")) || vasePracticed; break;
                case 7: done = mode == null && vasePracticed; break;
                case 8: done = catMemory.pets > 0; break;
                case 9: done = mode == null && tinPracticed; break;
                case 10: if (input.confirm && mode == null) FinishPractice(); break;
            }
            tutorialLastDoor = door.progress;
            if (!done) return;
            tutorialStep = Mathf.Min(TutorialStepCount - 1, tutorialStep + 1);
            tutorialHold = 0; tutorialFeedback = "做到了。换个情境，再用同样的动作。"; tutorialFeedbackLeft = 2.5f;
            Say(TutorialTitle(), "good");
        }

        public bool FinishPractice()
        {
            if (levelIndex != 3 || tutorialStep != 10 || mode != null || status != "playing") return false;
            Finish(true, "基础动作练习完成。正式夜晚里，先观察，再决定怎么运用。");
            return true;
        }
    }
}
