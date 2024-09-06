using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Celeste.Mod.Core;
using Monocle;
using MonoMod.Cil;
using System.Text.RegularExpressions;
using System.Linq;
using Celeste;

public class SaveMerger {

	public static bool MergeSaves(Celeste.SaveData baseSave, Celeste.SaveData otherSave) {
		if (baseSave.Version != otherSave.Version || baseSave.DebugMode || otherSave.DebugMode) {
			// probably bad
			return false;
		}
		// string Name = "Madeline";
		// long Time;
		baseSave.Time += otherSave.Time;
		// DateTime LastSave;
		if (otherSave.LastSave > baseSave.LastSave) {
			baseSave.LastSave = otherSave.LastSave;
			// could also set it to DateTime.Now, idk
		}
		// bool CheatMode;
		// bool AssistMode;
		// bool VariantMode;
		// Celeste.Assists Assists;
		// string TheoSisterName;
		// int UnlockedAreas;
		baseSave.UnlockedAreas = Math.Min(baseSave.UnlockedAreas, otherSave.UnlockedAreas);
		// int TotalDeaths;
		baseSave.TotalDeaths += otherSave.TotalDeaths;
		// annoyin
		// int TotalStrawberries;
		// int TotalGoldenStrawberries;
		// int TotalJumps;
		// int TotalWallJumps;
		// int TotalDashes;
		baseSave.TotalJumps += otherSave.TotalJumps;
		baseSave.TotalWallJumps += otherSave.TotalWallJumps;
		baseSave.TotalDashes += otherSave.TotalDashes;
		// HashSet<string> Flags = new HashSet<string>();
		baseSave.Flags.UnionWith(otherSave.Flags);
		// List<string> Poem;
		// bool[] SummitGems;
		for(int i = 0; i < baseSave.SummitGems.Length; i++) {
			baseSave.SummitGems[i] |= otherSave.SummitGems[i];
		}
		baseSave.RevealedChapter9 |= otherSave.RevealedChapter9;
		// Celeste.AreaKey LastArea;
		// Celeste.Session CurrentSession;
		// List<Celeste.AreaStats> Areas;
		List<Celeste.AreaStats> toAdd = new List<AreaStats>();
		foreach (Celeste.AreaStats otherArea in otherSave.Areas)
		{
			bool merged = false;
			foreach (Celeste.AreaStats baseArea in baseSave.Areas)
			{
				merged |= MergeAreaStats(baseArea, otherArea);
			}
			if (!merged)
			{
				toAdd.Add(otherArea);
			}
		}
		//TODO do we need to add this for vanilla?
		foreach (var area in toAdd)
		{
			baseSave.Areas.Add(area.Clone());
		}
		baseSave.Poem = baseSave.Poem.Union(otherSave.Poem).ToList();
		baseSave.TotalStrawberries = baseSave.Areas.Sum(area => area.TotalStrawberries);
		// baseSave.TotalGoldenStrawberries = baseSave.Areas.Sum(area => area.Modes.Sum(mode => mode.Strawberries.Sum(strawb =>{
		// 	AreaData areaData = 
		// } )));
		// int FileSlot;
		//TODO: What is this
		// bool DoNotSave;

		// Probably the start of modded stuff?
		// List<Celeste.LevelSetStats> LevelSets;
		List<Celeste.LevelSetStats> toAddSet = new List<LevelSetStats>();
		foreach (var otherSets in otherSave.LevelSets.Union(otherSave.LevelSetRecycleBin)) {
			bool merged = false;
			foreach (var baseSets in baseSave.LevelSets.Union(baseSave.LevelSetRecycleBin)) {
				merged |= MergeLevelSetStats(baseSets, otherSets);
			}
			if(!merged) {
				toAddSet.Add(otherSets);
			}
		}
		// don't need to add straightto recycle bin, should be fine
		foreach (var set in toAddSet) {
			baseSave.LevelSets.Add(set);
		}
		// bool HasModdedSaveData;
		baseSave.HasModdedSaveData |= otherSave.HasModdedSaveData;
		// Celeste.AreaKey LastArea_Safe;
		// Celeste.Session CurrentSession_Safe;
		return true;
	}

	public static bool MergeLevelSetStats(Celeste.LevelSetStats baseStats, Celeste.LevelSetStats otherStats) {
		// List<Celeste.AreaStats> Areas;
		if(baseStats.Name != otherStats.Name) {
			return false;
		}
		List<Celeste.AreaStats> toAdd = new List<AreaStats>();
		foreach (Celeste.AreaStats otherArea in otherStats.Areas ) {
			bool merged = false;
			foreach (Celeste.AreaStats baseArea in baseStats.Areas) {
				merged |= MergeAreaStats(baseArea, otherArea);
			}
			if (!merged) {
				toAdd.Add(otherArea);
			}
		}
		foreach (var area in toAdd) {
			baseStats.Areas.Add(area.Clone());
		}
		baseStats.Poem = baseStats.Poem.Union(otherStats.Poem).ToList();
		return false;
	// public List<string> Poem = new List<string>();
}

	public static bool MergeAreaStats(Celeste.AreaStats baseStats, Celeste.AreaStats otherStats)
	{
		if(baseStats.ID != otherStats.ID) {
			return false;
		}
		
		for(int i = 0; i < baseStats.Modes.Length; i++) {
			baseStats.Modes[i].Strawberries.UnionWith(otherStats.Modes[i].Strawberries);
			baseStats.Modes[i].Checkpoints.UnionWith(otherStats.Modes[i].Checkpoints);
			baseStats.Modes[i].TotalStrawberries = baseStats.Modes[i].Strawberries.Count;
			baseStats.Modes[i].Completed |= otherStats.Modes[i].Completed;
			baseStats.Modes[i].SingleRunCompleted |= otherStats.Modes[i].SingleRunCompleted;
			baseStats.Modes[i].FullClear |= otherStats.Modes[i].FullClear;
			baseStats.Modes[i].Deaths += otherStats.Modes[i].Deaths;
			baseStats.Modes[i].TimePlayed += otherStats.Modes[i].TimePlayed;
			baseStats.Modes[i].BestTime = Math.Min(baseStats.Modes[i].BestTime, otherStats.Modes[i].BestTime);
			baseStats.Modes[i].BestFullClearTime = Math.Min(baseStats.Modes[i].BestFullClearTime, otherStats.Modes[i].BestFullClearTime);
			baseStats.Modes[i].BestDashes = Math.Min(baseStats.Modes[i].BestDashes, otherStats.Modes[i].BestDashes);
			baseStats.Modes[i].BestDeaths = Math.Min(baseStats.Modes[i].BestDeaths, otherStats.Modes[i].BestDeaths);
			baseStats.Modes[i].HeartGem |= otherStats.Modes[i].HeartGem;
		}
		return true;
	}
}
