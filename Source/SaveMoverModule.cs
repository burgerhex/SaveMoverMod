﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Celeste.Mod.Core;
using Monocle;
using MonoMod.Cil;
using System.Text.RegularExpressions;
using System.Linq;
using Microsoft.Xna.Framework;

namespace Celeste.Mod.SaveMover;

public class SaveMoverModule : EverestModule {
    public static SaveMoverModule Instance { get; private set; }

    public override Type SettingsType => typeof(SaveMoverModuleSettings);
    public static SaveMoverModuleSettings Settings => (SaveMoverModuleSettings) Instance._Settings;

    public override Type SessionType => typeof(SaveMoverModuleSession);
    public static SaveMoverModuleSession Session => (SaveMoverModuleSession) Instance._Session;

    public override Type SaveDataType => typeof(SaveMoverModuleSaveData);
    public static SaveMoverModuleSaveData SaveData => (SaveMoverModuleSaveData) Instance._SaveData;

    public bool IsMoving;
    public SaveMoverModule() {
        Instance = this;
#if DEBUG
        // debug builds use verbose logging
        Logger.SetLogLevel(nameof(SaveMoverModule), LogLevel.Verbose);
#else
        // release builds use info logging to reduce spam in log files
        Logger.SetLogLevel(nameof(SaveMoverModule), LogLevel.Verbose);
#endif
        IsMoving = false;
    }

    public override void Load() {
        // TODO: apply any hooks that should always be active
        // IL.Celeste.OuiFileSelectSlot.Setup += onOuiFileSelectSetup;
        Logger.Log(nameof(SaveMoverModule), "Loading Hooks");
        On.Celeste.OuiFileSelect.Update += onFileSelectUpdate;
        On.Celeste.Overworld.InputEntity.Render += InputEntityRender;
        Logger.Log(nameof(SaveMoverModule), "Hooks Loaded");

    }


    public override void Unload() {
        // TODO: unapply any hooks applied in Load()
        On.Celeste.OuiFileSelect.Update -= onFileSelectUpdate;
    }

    #region Hook Definitions
    private void InputEntityRender(On.Celeste.Overworld.InputEntity.orig_Render orig, Entity self)
    {
        // This should always work? idk how self can be anything else
        Overworld.InputEntity inputEntity = (Overworld.InputEntity)self;
        if(!(inputEntity.Overworld.Current is OuiFileSelect)) {
            orig(self);
            return;
        }
        float inputEase = inputEntity.Overworld.inputEase;
        if(inputEase <= 0) {
            orig(self);
            return;
        }
        var wiggler = inputEntity.Get<Wiggler>();
        OuiFileSelect fileSelect = (OuiFileSelect)inputEntity.Overworld.Current;
        
        var menuData = fileSelect.Get<DataComponent>();
        if (menuData is null) {
            fileSelect.Add(menuData = new DataComponent());
        }
        string dialogId = "MENU_MOVE_SAVE";
        if(menuData.IsMoving) {
            dialogId = "MENU_STOP_MOVING";
        } 
        
        string label = Dialog.Clean(dialogId);
        float buttonWidth = ButtonUI.Width(label, Settings.SaveMoverBind.Button);
        Vector2 position = new Vector2(40f, 1024f);
        position.X += buttonWidth*0.5f;
        ButtonUI.Render(position, label, Settings.SaveMoverBind.Button, 0.5f, 1f, wiggler.Value * 0.05f);
        orig(self);
    }

        private static void onFileSelectUpdate(On.Celeste.OuiFileSelect.orig_Update orig, OuiFileSelect self) {
            // make sure vanilla portraits are loaded (in case the player played a map with a custom Portraits.xml).
            // GFX.PortraitsSpriteBank = new SpriteBank(GFX.Portraits, Path.Combine("Graphics", "Portraits.xml"));
            var menuData = self.Get<DataComponent>();
            if (menuData is null) {
                self.Add(menuData = new DataComponent());
            } 

            if (self.Focused && Settings.SaveMoverBind.Pressed) {
                if (menuData.IsMoving){
                    Audio.Play("event:/ui/main/button_toggle_off");
                    menuData.IsMoving = false;
                    // Copy to temp

                    // Glob all mod files
                    string searchDirectory = UserIO.SavePath.Replace("\\", "/");
                    DirectoryInfo dir = new DirectoryInfo(searchDirectory);

                    List<Tuple<string, string>> movedFiles = new List<Tuple<string, string>>();  
                    foreach (var file in dir.EnumerateFiles($"{menuData.StartSlot}-*.celeste").Concat(dir.EnumerateFiles($"{menuData.StartSlot}.celeste"))) {
                        var regex = new Regex(Regex.Escape($"{menuData.StartSlot}"));
                        string movedPath = regex.Replace(file.Name, $"{self.SlotIndex}", 1);
                        string tempPath = searchDirectory + $"/temp_{file.Name}";
                        movedFiles.Add(new Tuple<string, string>(tempPath, searchDirectory + $"/{movedPath}"));
                        File.Move(searchDirectory + $"/{file.Name}", tempPath);
                    }

                    if (menuData.StartSlot > self.SlotIndex) {
                        for (int i = menuData.StartSlot-1; i >= self.SlotIndex; i--) {
                            foreach(var file in dir.EnumerateFiles($"{i}-*.celeste").Concat(dir.EnumerateFiles($"{i}.celeste"))) {
                                var regex = new Regex(Regex.Escape($"{i}"));
                                string movedPath = regex.Replace(file.Name, $"{i+1}", 1);
                                File.Move(searchDirectory + $"/{file.Name}", searchDirectory + $"/{movedPath}");
                            }
                        }
                    } else if (menuData.StartSlot < self.SlotIndex) {
                        for (int i = menuData.StartSlot; i < self.SlotIndex; i++) {
                            foreach(var file in dir.EnumerateFiles($"{i+1}-*.celeste").Concat(dir.EnumerateFiles($"{i+1}.celeste"))) {
                                var regex = new Regex(Regex.Escape($"{i+1}"));
                                string movedPath = regex.Replace(file.Name, $"{i}", 1);
                                File.Move(searchDirectory + $"/{file.Name}", searchDirectory + $"/{movedPath}");
                            }
                        }
                    }
                    foreach( var filePair in movedFiles) {
                        File.Move(filePair.Item1, filePair.Item2);
                    }

                }
                else if (self.SlotIndex < self.Slots.Length - 1) {
                    Audio.Play("event:/ui/main/button_toggle_on");
                    menuData.IsMoving = true;
                    menuData.StartSlot = self.SlotIndex;
                }

            }
            // We're just going to recreate the menu update logic if we're moving
            if (menuData.IsMoving) {
                // (self as Oui).Update();
                if (!self.Focused)  {
                    menuData.IsMoving = false;
                    return;
                }
                if (!self.SlotSelected) {
                    if (Input.MenuUp.Pressed && self.SlotIndex > 0) {
                        Audio.Play("event:/ui/main/savefile_rollover_up");
                        OuiFileSelectSlot prev = self.Slots[self.SlotIndex - 1];
                        OuiFileSelectSlot curr = self.Slots[self.SlotIndex];
                        curr.FileSlot--;
                        prev.FileSlot++;
                        self.Slots[self.SlotIndex] = prev;
                        self.Slots[self.SlotIndex-1] = curr;
                        self.SlotIndex--;
                        foreach (OuiFileSelectSlot slot in self.Slots) {
                            // if (slot == curr) continue;
                            slot.MoveTo(slot.IdlePosition.X, slot.IdlePosition.Y);
                        }
                    }
                    else if (Input.MenuDown.Pressed && self.SlotIndex < self.Slots.Length - 2)  {
                        Audio.Play("event:/ui/main/savefile_rollover_down");
                        OuiFileSelectSlot next = self.Slots[self.SlotIndex + 1];
                        OuiFileSelectSlot curr = self.Slots[self.SlotIndex];
                        curr.FileSlot++;
                        next.FileSlot--;
                        self.Slots[self.SlotIndex] = next;
                        self.Slots[self.SlotIndex+1] = curr;
                        self.SlotIndex++;
                        foreach (OuiFileSelectSlot slot in self.Slots) {
                            // if (slot == curr) continue;
                            slot.MoveTo(slot.IdlePosition.X, slot.IdlePosition.Y);
                        }
                    }
                    else if (Input.MenuCancel.Pressed) {
                        Audio.Play("event:/ui/main/button_back");
                         (self as Oui).Overworld.Goto<OuiMainMenu>();
                    }
                }
                else if (Input.MenuCancel.Pressed && !self.HasSlots && !self.Slots[self.SlotIndex].StartingGame)   {
                    Audio.Play("event:/ui/main/button_back");
                     (self as Oui).Overworld.Goto<OuiMainMenu>();
                }
            } else {
                orig(self);
            }
        }


    #endregion

    private class DataComponent : Component {
            public bool IsMoving;
            public int StartSlot;
            public DataComponent() : base(false, false) { IsMoving = false; }
    }
}