#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace BlockShooter.Editor
{
    public static class LevelEditorColorUtility
    {
        public static Color GetColor(GameConfig gameCfg, BlockColorType t)
        {
            if (gameCfg != null)
            {
                return gameCfg.GetColor(t);
            }
            return Color.white;
        }

        public static (BlockColorType t, Color c, string n)[] GetActiveColors(GameConfig gameCfg)
        {
            if (gameCfg != null && gameCfg.colors != null && gameCfg.colors.Count > 0)
            {
                var list = new List<(BlockColorType, Color, string)>();
                foreach (var def in gameCfg.colors)
                {
                    if (def == null) continue;
                    list.Add((def.colorType, gameCfg.GetColor(def.colorType), def.displayName));
                }
                return list.ToArray();
            }

            return new[]
            {
                (BlockColorType.Red,    new Color(.90f,.20f,.20f), "Red"   ),
                (BlockColorType.Blue,   new Color(.20f,.50f,.90f), "Blue"  ),
                (BlockColorType.Green,  new Color(.20f,.80f,.30f), "Green" ),
                (BlockColorType.Yellow, new Color(1.00f,.85f,.10f),"Yellow"),
                (BlockColorType.Purple, new Color(.60f,.20f,.90f), "Purple"),
                (BlockColorType.Orange, new Color(1.00f,.55f,.10f),"Orange"),
            };
        }

        public static BlockColorType DrawColorPopup(GameConfig gameCfg, BlockColorType selected, params GUILayoutOption[] options)
        {
            var pal = GetActiveColors(gameCfg);
            string[] names = pal.Select(x => x.n).ToArray();
            int index = System.Array.FindIndex(pal, x => x.t == selected);
            if (index < 0) index = 0;

            int newIndex = EditorGUILayout.Popup(index, names, options);
            return pal[newIndex].t;
        }

        public static BlockColorType DrawColorPopup(GameConfig gameCfg, Rect rect, BlockColorType selected)
        {
            var pal = GetActiveColors(gameCfg);
            string[] names = pal.Select(x => x.n).ToArray();
            int index = System.Array.FindIndex(pal, x => x.t == selected);
            if (index < 0) index = 0;

            int newIndex = EditorGUI.Popup(rect, index, names);
            return pal[newIndex].t;
        }
    }
}
#endif
