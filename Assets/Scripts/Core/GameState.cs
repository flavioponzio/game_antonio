using System;
using System.Collections.Generic;
using RecreioEspacial.Data;
using UnityEngine;

namespace RecreioEspacial.Core
{
    /// <summary>
    /// Estado do jogo: cena atual, flags, inventário, item selecionado e respostas de puzzles.
    /// É tudo que precisa ser salvo para "Continuar" (exceto o que é só da sessão: busy, dica, etc.).
    /// </summary>
    public class GameState
    {
        public string Scene = "title";
        public readonly Dictionary<string, string> Flags = new Dictionary<string, string>();
        public readonly List<string> Inventory = new List<string>();
        public readonly Dictionary<string, int?[]> PuzzleAnswers = new Dictionary<string, int?[]>();
        public float PlaySeconds;

        // Estado de sessão (não é salvo)
        public string SelectedItem;
        public bool Busy;
        public int HintLevel;
        public float IdleSeconds;

        public event Action Changed;
        public void NotifyChanged() => Changed?.Invoke();

        public bool Flag(string key) => key != null && Flags.ContainsKey(key);
        public string FlagValue(string key) => key != null && Flags.TryGetValue(key, out var v) ? v : null;

        public void SetFlag(string key, string value = "true")
        {
            if (string.IsNullOrEmpty(key)) return;
            if (value == null || value == "false") Flags.Remove(key);
            else Flags[key] = value;
            NotifyChanged();
        }

        public void UnsetFlag(string key)
        {
            if (key != null && Flags.Remove(key)) NotifyChanged();
        }

        public bool Has(string item) => Inventory.Contains(item);

        public void Give(string item)
        {
            if (string.IsNullOrEmpty(item) || Inventory.Contains(item)) return;
            Inventory.Add(item);
            NotifyChanged();
        }

        public bool Check(Condition c)
        {
            if (c == null) return true;
            foreach (var f in c.Flags) if (!Flag(f)) return false;
            foreach (var f in c.NotFlags) if (Flag(f)) return false;
            foreach (var i in c.Has) if (!Has(i)) return false;
            foreach (var i in c.NotHas) if (Has(i)) return false;
            return true;
        }

        public int?[] AnswersFor(string puzzleId, int count)
        {
            if (!PuzzleAnswers.TryGetValue(puzzleId, out var a) || a.Length != count)
            {
                a = new int?[count];
                PuzzleAnswers[puzzleId] = a;
            }
            return a;
        }

        public void ResetProgress()
        {
            Flags.Clear();
            Inventory.Clear();
            PuzzleAnswers.Clear();
            SelectedItem = null;
            HintLevel = 0;
            IdleSeconds = 0;
            PlaySeconds = 0;
            NotifyChanged();
        }

        // ---------- Salvamento (PlayerPrefs) ----------

        const string SaveKey = "RecreioEspacial.EP01.save";

        [Serializable]
        class SaveData
        {
            public string scene;
            public List<string> flagKeys = new List<string>();
            public List<string> flagValues = new List<string>();
            public List<string> inventory = new List<string>();
            public List<string> puzzleIds = new List<string>();
            public List<string> puzzleAnswers = new List<string>();
            public float playSeconds;
        }

        public static bool HasSave => PlayerPrefs.HasKey(SaveKey);

        public static void DeleteSave()
        {
            PlayerPrefs.DeleteKey(SaveKey);
            PlayerPrefs.Save();
        }

        public void Save()
        {
            var d = new SaveData { scene = Scene, playSeconds = PlaySeconds };
            foreach (var kv in Flags) { d.flagKeys.Add(kv.Key); d.flagValues.Add(kv.Value); }
            d.inventory.AddRange(Inventory);
            foreach (var kv in PuzzleAnswers)
            {
                d.puzzleIds.Add(kv.Key);
                var parts = new string[kv.Value.Length];
                for (int i = 0; i < parts.Length; i++) parts[i] = kv.Value[i]?.ToString() ?? "";
                d.puzzleAnswers.Add(string.Join(",", parts));
            }
            PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(d));
            PlayerPrefs.Save();
        }

        public bool Load()
        {
            if (!HasSave) return false;
            SaveData d;
            try { d = JsonUtility.FromJson<SaveData>(PlayerPrefs.GetString(SaveKey)); }
            catch (Exception e) { Debug.LogWarning("Save inválido, ignorando: " + e.Message); return false; }
            if (d == null || string.IsNullOrEmpty(d.scene)) return false;

            ResetProgress();
            Scene = d.scene;
            PlaySeconds = d.playSeconds;
            for (int i = 0; i < d.flagKeys.Count && i < d.flagValues.Count; i++) Flags[d.flagKeys[i]] = d.flagValues[i];
            Inventory.AddRange(d.inventory);
            for (int i = 0; i < d.puzzleIds.Count && i < d.puzzleAnswers.Count; i++)
            {
                var parts = d.puzzleAnswers[i].Split(',');
                var a = new int?[parts.Length];
                for (int k = 0; k < parts.Length; k++) a[k] = int.TryParse(parts[k], out var v) ? v : (int?)null;
                PuzzleAnswers[d.puzzleIds[i]] = a;
            }
            NotifyChanged();
            return true;
        }
    }
}
