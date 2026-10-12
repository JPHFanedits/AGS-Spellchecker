using System;
using System.Collections.Generic;
using System.Drawing;
using System.Xml;
using AGS.Types;

namespace AGSSpellchecker {
[RequiredAGSVersion("3.6.1.0")]
public sealed class Plugin : IAGSEditorPlugin {
 readonly SpellcheckerComponent component;
 public Plugin(IAGSEditor editor){component=new SpellcheckerComponent(editor);editor.AddComponent(component);}
 public void Dispose(){component.EditorShutdown();}
}
public sealed class SpellcheckerComponent : IEditorComponent {
 readonly Icon icon;readonly IAGSEditor editor;ContentDocument document;ReviewPanel panel;MenuCommands menu;bool disposed;
 public string ComponentID {get{return "AGSSpellchecker";}}
 public SpellcheckerComponent(IAGSEditor editor){this.editor=editor;using(var stream=typeof(Plugin).Assembly.GetManifestResourceStream("AGSSpellchecker.Icon")){using(var resource=new Icon(stream)){icon=(Icon)resource.Clone();}}editor.GUIController.RegisterIcon("AGSSpellchecker.Icon",icon);editor.GUIController.ProjectTree.AddTreeRoot(this,"AGSSpellchecker","AGS Spellchecker","AGSSpellchecker.Icon");editor.GUIController.AddMenu(this,"AGSSpellchecker.Menu","Spellchecker",editor.GUIController.FileMenuID);menu=new MenuCommands("AGSSpellchecker.Menu");menu.Commands.Add(editor.GUIController.CreateMenuCommand(this,"Open","Open AGS Spellchecker"));editor.GUIController.AddMenuItems(this,menu);}
 public void CommandClick(string command){if(disposed)return;if(panel==null||panel.IsDisposed){panel=new ReviewPanel(new EditorBridge(editor));document=new ContentDocument(panel,"AGS Spellchecker",this,"AGSSpellchecker.Icon");document.TreeNodeID="AGSSpellchecker";}editor.GUIController.AddOrShowPane(document);panel.RefreshProject();}
 public IList<MenuCommand> GetContextMenu(string node){return new List<MenuCommand>{new MenuCommand("Open","Open AGS Spellchecker")};}
 public void PropertyChanged(string propertyName,object oldValue){}
 public void BeforeSaveGame(){}
 public void RefreshDataFromGame(){if(panel!=null&&!panel.IsDisposed)panel.ResetProject();}
 public void GameSettingsChanged(){}
 public void ToXml(XmlTextWriter writer){}
 public void FromXml(XmlNode node){}
 public void EditorShutdown(){if(disposed)return;disposed=true;if(document!=null){editor.GUIController.RemovePaneIfExists(document);document.Dispose();}if(menu!=null)editor.GUIController.RemoveMenuItems(menu);icon.Dispose();}
}
}

