using System;
using System.Collections.Generic;
using System.Drawing.Printing;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using LeagueSharp;
using LeagueSharp.Common;
using SharpDX;
using Color = System.Drawing.Color;

namespace GragasQ
{
    internal class Program
    {
        public const string ChampionName = "Gragas";

        // Orbwalker instance
        public static Orbwalking.Orbwalker Orbwalker;

        // Spells
        public static List<Spell> SpellList = new List<Spell>();

        public static Spell Q;
        public static Spell W;
        public static Spell E;
        public static Spell R;

        //
        public static Items.Item DfgItem;
        public static SpellSlot IgniteSpellSlot;

        // Barrel
        private static Obj_GeneralParticleEmmiter barrel = null;

        // Menu
        public static Menu Config;

        private static Obj_AI_Hero Player;

        private static bool AttacksEnabled
        {
            get
            {
                if (W.IsCharging)
                    return false;

                if (Config.Item("ComboActive").GetValue<KeyBind>().Active)
                    return (!Q.IsReady() && !W.IsReady() && !E.IsReady());

                return true;
            }
        }

        private static void Main(string[] args)
        {
            CustomEvents.Game.OnGameLoad += Game_OnGameLoad;
        }

        private static void Game_OnGameLoad(EventArgs args)
        {
            Player = ObjectManager.Player;

            if (Player.ChampionName != ChampionName) return;

            // Initialize the spells
            Q = new Spell(SpellSlot.Q, 1100);
            W = new Spell(SpellSlot.W);
            E = new Spell(SpellSlot.E, 800);
            R = new Spell(SpellSlot.R, 1050);

            Q.SetSkillshot(0.25f, 275f, 1300f, false, SkillshotType.SkillshotCircle);
            E.SetSkillshot(0, 200f, 1200f, true, SkillshotType.SkillshotLine);
            R.SetSkillshot(0.25f, 375f, 1800f, false, SkillshotType.SkillshotCircle);

            SpellList.Add(Q);
            SpellList.Add(E);
            SpellList.Add(R);

            IgniteSpellSlot = Player.GetSpellSlot("SummonerDot");
            DfgItem = Utility.Map.GetMap()._MapType == Utility.Map.MapType.TwistedTreeline ||
                  Utility.Map.GetMap()._MapType == Utility.Map.MapType.CrystalScar
                ? new Items.Item(3188, 750)
                : new Items.Item(3128, 750);

            // Create the menu
            Config = new Menu(ChampionName + "Q", ChampionName + "Q", true);

            // Orbwalker submenu
            Config.AddSubMenu(new Menu("Orbwalking", "Orbwalking"));

            // Add the target seletor to the menu as submenu
            var targetSelectorMenu = new Menu("Target Selector", "Target Selector");
            SimpleTs.AddToMenu(targetSelectorMenu);
            Config.AddSubMenu(targetSelectorMenu);

            // Load the orbwalker and add it to the menu as submenu
            Orbwalker = new Orbwalking.Orbwalker(Config.SubMenu("Orbwalking"));

            // Combo Menu
            Config.AddSubMenu(new Menu("Combo", "Combo"));
            Config.SubMenu("Combo").AddItem(new MenuItem("UseQCombo", "Use Q").SetValue(true));
            Config.SubMenu("Combo").AddItem(new MenuItem("UseWCombo", "Use W").SetValue(true));
            Config.SubMenu("Combo").AddItem(new MenuItem("UseECombo", "Use E").SetValue(true));
            Config.SubMenu("Combo").AddItem(new MenuItem("UseRCombo", "Use R").SetValue(true));
            Config.SubMenu("Combo").AddItem(new MenuItem("UseIgniteCombo", "Use Ignite").SetValue(true));
            Config.SubMenu("Combo").AddItem(new MenuItem("UseDfgCombo", "Use Dfg").SetValue(true));
            Config.SubMenu("Combo")
                .AddItem(new MenuItem("ComboActive", "Combo!").SetValue(
                    new KeyBind(Config.Item("Orbwalk").GetValue<KeyBind>().Key, KeyBindType.Press)));

            // Harass Menu
            Config.AddSubMenu(new Menu("Harass", "Harass"));
            Config.SubMenu("Harass").AddItem(new MenuItem("UseQHarass", "Use Q").SetValue(true));
            Config.SubMenu("Harass").AddItem(new MenuItem("UseEHarass", "Use E").SetValue(false));
            Config.SubMenu("Harass")
                .AddItem(
                    new MenuItem("HarassActive", "Harass!").SetValue(
                        new KeyBind(Config.Item("Farm").GetValue<KeyBind>().Key, KeyBindType.Press)));
            Config.SubMenu("Harass")
                .AddItem(
                    new MenuItem("HarassActiveT", "Harass (toggle)!").SetValue(new KeyBind("Y".ToCharArray()[0],
                        KeyBindType.Toggle)));


            // Misc
            Config.AddSubMenu(new Menu("Misc", "Misc"));
            Config.SubMenu("Misc").AddItem(new MenuItem("InterruptSpells", "Interrupt spells").SetValue(true));
            Config.SubMenu("Misc").AddItem(new MenuItem("QExplode", "Auto barrel explode").SetValue(true));

            // Drawings menu
            Config.AddSubMenu(new Menu("Drawings", "Drawings"));
            Config.SubMenu("Drawings")
                .AddItem(
                    new MenuItem("QRange", "Q range").SetValue(new Circle(true, Color.FromArgb(150, Color.IndianRed))));
            Config.SubMenu("Drawings")
                .AddItem(
                    new MenuItem("ERange", "E range").SetValue(new Circle(true, Color.FromArgb(150, Color.MediumPurple))));
            Config.SubMenu("Drawings")
               .AddItem(
                new MenuItem("RRange", "R range").SetValue(new Circle(true, Color.FromArgb(150, Color.DarkRed))));
            Config.AddToMainMenu();

            // Add the events we are going to use:
            Game.OnGameUpdate += Game_OnGameUpdate;
            Drawing.OnDraw += Drawing_OnDraw;
            Interrupter.OnPosibleToInterrupt += Interrupter_OnPosibleToInterrupt;
            Game.PrintChat("<b>" + ChampionName + "Q</b> Loaded!");
            Orbwalking.BeforeAttack += Orbwalking_BeforeAttack;
            GameObject.OnCreate += GameObject_OnCreate;
            GameObject.OnDelete += GameObject_OnDelete;
        }

        static void GameObject_OnDelete(GameObject sender, EventArgs args)
        {
            if (!sender.IsValid || !(sender is Obj_GeneralParticleEmmiter))
            {
                return; //not sure if needed
            }

            var obj = (Obj_GeneralParticleEmmiter)sender;
            if (obj.Name == "Gragas_Base_Q_Ally.troy" || obj.Name == "Gragas_Base_Q_End.troy")
            {
                barrel = null;
            }
        }

        static void GameObject_OnCreate(GameObject sender, EventArgs args)
        {
            if (!sender.IsValid || !(sender is Obj_GeneralParticleEmmiter))
            {
                return; //not sure if needed
            }

            var obj = (Obj_GeneralParticleEmmiter)sender;
            if (obj.Name == "Gragas_Base_Q_Ally.troy")
            {
                barrel = obj;
            }
        }

        static void Orbwalking_BeforeAttack(Orbwalking.BeforeAttackEventArgs args)
        {
            args.Process = AttacksEnabled;
        }

        static void Interrupter_OnPosibleToInterrupt(Obj_AI_Base unit, InterruptableSpell spell)
        {
            if (!Config.Item("InterruptSpells").GetValue<bool>()) return;

            if (Player.Distance(unit) < R.Range)
            {
                R.Cast(unit);
            }
            if (Player.Distance(unit) < E.Range)
            {
                E.Cast(unit);
            }
        }


        static void Drawing_OnDraw(EventArgs args)
        {
            //Draw the ranges of the spells.
            foreach (var spell in SpellList)
            {
                var menuItem = Config.Item(spell.Slot + "Range").GetValue<Circle>();
                if (menuItem.Active && spell.Slot != SpellSlot.W)
                    Utility.DrawCircle(Player.Position, spell.Range - spell.Width, menuItem.Color);
            }
        }

        static void Game_OnGameUpdate(EventArgs args)
        {
            if (Player.IsDead) return;

            Orbwalker.SetMovement(true);

            if (Config.Item("QExplode").GetValue<bool>())
            {
                BarrelExplode();
            }

            if (Config.Item("ComboActive").GetValue<KeyBind>().Active)
            {
                Combo();
            }
            else
            {
                if (Config.Item("HarassActive").GetValue<KeyBind>().Active ||
                   Config.Item("HarassActiveT").GetValue<KeyBind>().Active)
                    Harass();
            }

        }

        private static void BarrelExplode()
        {
            if (barrel != null)
            {
                foreach (var enemy in ObjectManager.Get<Obj_AI_Hero>().Where(enemy => Vector3.Distance(barrel.Position, enemy.Position) < barrel.BoundingRadius + enemy.BoundingRadius && enemy.IsEnemy && enemy.IsValid && !enemy.IsDead))
                {
                    float rangeAllow = barrel.BoundingRadius + enemy.BoundingRadius - 25;

                    if (enemy.IsDashing() || 
                        Vector3.Distance(barrel.Position, enemy.Position) > rangeAllow)
                    {
                        Q.Cast();
                    }
                }
            }
        }

        private static void Combo()
        {
            UseSpells(Config.Item("UseQCombo").GetValue<bool>(), Config.Item("UseWCombo").GetValue<bool>(),
                Config.Item("UseECombo").GetValue<bool>(), Config.Item("UseRCombo").GetValue<bool>(), Config.Item("UseIgniteCombo").GetValue<bool>(), Config.Item("UseDfgCombo").GetValue<bool>());
        }

        private static void Harass()
        {
            UseSpells(Config.Item("UseQHarass").GetValue<bool>(), false, Config.Item("UseEHarass").GetValue<bool>(),
                false,false,false);
        }

        private static void UseSpells(bool useQ, bool useW, bool useE, bool useR, bool useIgnite, bool useDFG)
        {
            var qTarget = SimpleTs.GetTarget(Q.Range, SimpleTs.DamageType.Magical);
            var eTarget = SimpleTs.GetTarget(E.Range, SimpleTs.DamageType.Magical);
            var rTarget = SimpleTs.GetTarget(R.Range, SimpleTs.DamageType.Magical);
            var igniteTarget = SimpleTs.GetTarget(600, SimpleTs.DamageType.Magical);

            // Auto ignite
            if (useIgnite && IgniteSpellSlot != SpellSlot.Unknown &&
                Player.SummonerSpellbook.CanUseSpell(IgniteSpellSlot) == SpellState.Ready &&
                igniteTarget != null && ObjectManager.Player.GetSummonerSpellDamage(igniteTarget, Damage.SummonerSpell.Ignite) > igniteTarget.Health)
            {
                Player.SummonerSpellbook.CastSpell(IgniteSpellSlot, rTarget);
            }

            
            if (qTarget != null && useQ && Q.IsReady())
            {
                if (Player.Distance(qTarget) < Q.Range)
                    Q.Cast(qTarget, false, true);
            }

            if (eTarget != null && useE && E.IsReady())
            {
                if (useW && W.IsReady())
                    W.Cast();
                else
                    E.Cast(eTarget);
            }

            if (!E.IsReady())
                if (useW && W.IsReady())
                    W.Cast();

            if (rTarget != null && useR && R.IsReady())
            {
                double extraDmg = 0;
                double dfgDmg = 1.0;

                if (useDFG && DfgItem.IsReady() && Player.Distance(rTarget.ServerPosition) < DfgItem.Range)
                {
                    extraDmg += Player.GetItemDamage(rTarget, Damage.DamageItems.Dfg);
                    dfgDmg = 1.2;
                }
                if (useIgnite && IgniteSpellSlot != SpellSlot.Unknown &&
                    Player.SummonerSpellbook.CanUseSpell(IgniteSpellSlot) == SpellState.Ready)
                {
                    extraDmg += ObjectManager.Player.GetSummonerSpellDamage(rTarget, Damage.SummonerSpell.Ignite);
                }

                if (R.GetDamage(rTarget)*dfgDmg + extraDmg > rTarget.Health)
                {
                    CastIgniteDfg(rTarget, useIgnite, useDFG);
                    R.Cast(rTarget, false, true);
                }
                if (Q.IsReady() && 
                    Q.GetDamage(rTarget) * dfgDmg + R.GetDamage(rTarget) * dfgDmg + extraDmg > rTarget.Health)
                {
                    CastIgniteDfg(rTarget, useIgnite, useDFG);
                    Q.Cast(rTarget);
                    R.Cast(rTarget);
                }
                if (Q.IsReady() && E.IsReady())
                {
                    if (Player.Distance(rTarget) <= 500 &&
                        Q.GetDamage(eTarget) * dfgDmg + E.GetDamage(eTarget) * dfgDmg + R.GetDamage(eTarget) * dfgDmg + extraDmg > eTarget.Health)
                    {
                        CastIgniteDfg(rTarget, useIgnite, useDFG);
                        E.Cast(eTarget);
                        Q.Cast(qTarget);
                        R.Cast(rTarget);
                    }
                    if (Player.Distance(rTarget) > 500 && Player.Distance(rTarget) <= R.Range &&
                        Q.GetDamage(rTarget) + E.GetDamage(rTarget) * dfgDmg + R.GetDamage(rTarget) * dfgDmg + extraDmg > rTarget.Health)
                    {
                        CastIgniteDfg(rTarget, useIgnite, useDFG);
                        Vector3 rPos = R.GetPrediction(rTarget).CastPosition;
                        rPos = Player.ServerPosition + Vector3.Normalize(rPos) * (Player.Distance(rPos) + 300);
                        R.Cast(rPos);
                    }
                }
            }
        }



        public static void CastIgniteDfg(Obj_AI_Base rTarget, bool useIgnite, bool useDFG)
        {
            if (useDFG && DfgItem.IsReady() && Player.Distance(rTarget.ServerPosition) < DfgItem.Range)
            {
                DfgItem.Cast(rTarget);
            }
            if (useIgnite && IgniteSpellSlot != SpellSlot.Unknown &&
                Player.SummonerSpellbook.CanUseSpell(IgniteSpellSlot) == SpellState.Ready)
            {
                Player.SummonerSpellbook.CastSpell(IgniteSpellSlot, rTarget);
            }
        }
    }
}
