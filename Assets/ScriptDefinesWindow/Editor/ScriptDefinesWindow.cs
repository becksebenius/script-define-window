/*

The MIT License (MIT)

Copyright (c) 2014 Ryan Beck Sebenius

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE.

*/

using UnityEngine;
using UnityEditor;
using System;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Linq;

public class ScriptDefinesWindow : EditorWindow 
{

	[MenuItem("Window/Script Defines")]
	static void OpenScriptDefinesWindow ()
	{
		EditorWindow.GetWindow<ScriptDefinesWindow>(true);
	}
	
	[Serializable]
	public class Definition
	{
		public string name = "";
		public bool isEnabled = true;
		public List<BuildTargetGroup> groups = new List<BuildTargetGroup>();
	}
	
	[SerializeField]
	List<Definition> buffer = new List<Definition>();
	
	[SerializeField]
	bool firstEnable = true;
	
	[SerializeField]
	bool changed = false;
	
	[SerializeField]
	List<string> errors = new List<string>();
	
	[SerializeField]
	Vector2 scrollPosition;
	
	[SerializeField]
	string newDefine = "";
	
	void OnEnable ()
	{
		title = "Script Defines";
		if(firstEnable)
		{
			firstEnable = false;
			LoadBuffer();
		}
		Repaint();
	}
	
	void LoadBuffer ()
	{
		changed = false;
		buffer = new List<Definition>();
		
		var definitions = new Dictionary<string, Definition>();
		
		foreach(BuildTargetGroup grp in Enum.GetValues(typeof(BuildTargetGroup)))
		{
			var defines = PlayerSettings.GetScriptingDefineSymbolsForGroup(grp);
			
			var split = defines.Split(';');
			for(int i = 0; i < split.Length; i++)
			{
				var define = split[i];
				if(define.Length == 0)
				{
					continue;
				}
				
				bool isEnabled = true;
				bool isEnabledOnTarget = true;
				var name = define;
				
				var first = name[0];
				if(first == '*')
				{
					if(name.Length == 1) 
					{
						continue;
					}
					
					isEnabled = false;
					name = name.Substring(1, name.Length-1);
				}
				
				first = name[0];
				if(first == '|')
				{
					if(name.Length == 1)
					{
						continue;
					}
					
					isEnabledOnTarget = false;
					name = name.Substring(1, name.Length-1);
				}
				
				Definition definition;
				if(!definitions.TryGetValue(name, out definition))
				{
					definition = new Definition();
					definitions.Add(name, definition);
				}
				
				definition.isEnabled = isEnabled;
				definition.name = name;
				
				if(isEnabledOnTarget)
				{
					if(!definition.groups.Contains(grp))
					{
						definition.groups.Add(grp);
					}
				}
				
			}
		}
		
		buffer = definitions.Values.ToList();
		
		EditorGUIUtility.editingTextField = false;
	}
	
	void OnGUI ()
	{
		EditorGUILayout.BeginHorizontal();
		GUILayout.FlexibleSpace();
		
		GUI.enabled = this.changed
		&& errors.Count == 0 
		&& !EditorApplication.isCompiling;
		
		if(GUILayout.Button("Revert"))
		{
			LoadBuffer ();
		}
		if(GUILayout.Button("Apply"))
		{
			Apply();
			LoadBuffer();
		}
		GUI.enabled = true;
		EditorGUILayout.EndHorizontal();
		
		scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
		
		Rect r;
		
		bool changed = false;
		
		EditorGUI.BeginChangeCheck();
		for(int i = 0; i < buffer.Count; i++)
		{
			var define = buffer[i];
			
			r = EditorGUILayout.BeginHorizontal();
			define.isEnabled = EditorGUILayout.Toggle(define.isEnabled, GUILayout.Width(16));
			define.name = EditorGUILayout.TextField(define.name);
			
			GUILayout.Label("Platforms", GUILayout.Width(90));
			
			EditorGUILayout.EndHorizontal();
			
			r.x += r.width - 38;
			r.width = 16;
			if(!EditorGUI.Foldout(r, true, GUIContent.none))
			{
				GenericMenu menu = new GenericMenu();
				
				bool containsAll = true;
				bool containsAny = false;
				
				Action delayed = null;
				foreach(BuildTargetGroup type in Enum.GetValues(typeof(BuildTargetGroup)))
				{
					bool contains = define.groups.Contains(type);
					containsAll &= contains;
					containsAny |= contains;
					
					var t = type;
					delayed += ()=>{
						menu.AddItem(new GUIContent(Enum.GetName(typeof(BuildTargetGroup), t)), contains, ()=>{
							if(contains)
							{
								define.groups.Remove(t);
							}
							else
							{
								define.groups.Add(t);
							}
						});
					};
				}
				
				if(containsAll)
				{
					menu.AddDisabledItem(new GUIContent("All"));
				}
				else
				{
					menu.AddItem(new GUIContent("All"), false, ()=>{
						foreach(BuildTargetGroup type in Enum.GetValues(typeof(BuildTargetGroup)))
						{
							if(!define.groups.Contains(type))
							{
								define.groups.Add(type);
							}
						}
					});
				}
				
				if(!containsAny)
				{
					menu.AddDisabledItem(new GUIContent("None"));
				}
				else
				{
					menu.AddItem(new GUIContent("None"), false, ()=>{
						define.groups.Clear();
					});
				}
				
				menu.AddSeparator("");
				
				delayed ();
				
				menu.ShowAsContext();
			}
			
			r.width = 19;
			r.x += 16;
			GUIStyle style = new GUIStyle(GUI.skin.button);
			style.padding = new RectOffset(6, 0, 0, 2);
			style.alignment = TextAnchor.MiddleLeft;
			if(GUI.Button(r, "X", style))
			{
				EditorGUIUtility.editingTextField = false;
				buffer.RemoveAt(i--);
				continue;
			}
		}
		changed |= EditorGUI.EndChangeCheck();
		
		r = EditorGUILayout.BeginHorizontal();
		GUILayout.Space(24);
		newDefine = EditorGUILayout.TextField(newDefine);
		GUI.enabled = newDefine != "";
		if(GUILayout.Button("Add", GUILayout.Width(40), GUILayout.Height(14)))
		{
			var define = new Definition();
			define.isEnabled = true;
			define.name = newDefine;
			foreach(BuildTargetGroup type in Enum.GetValues(typeof(BuildTargetGroup)))
			{
				define.groups.Add(type);
			}
			buffer.Add(define);
			changed = true;
			newDefine = "";
		}
		GUI.enabled = true;
		GUILayout.Space(50);
		
		EditorGUILayout.EndHorizontal();
			
		EditorGUILayout.EndScrollView();
		
		if(errors.Count != 0)
		{
			GUILayout.Label("ERRORS:");
			GUIStyle errorStyle = new GUIStyle(GUI.skin.label);
			errorStyle.normal.textColor = Color.red;
			for(int i = 0; i < errors.Count; i++)
			{	
				var error = errors[i];
				GUILayout.Label(error, errorStyle);
			}
		}
		
		if(changed)
		{
			TestForErrors();
		}
		this.changed |= changed;
		
		
#if SCRIPT_DEFINES_TEST
		GUILayout.FlexibleSpace();
		GUILayout.Label("Script Defines Test is Enabled!");
#endif
	}
	
	void TestForErrors ()
	{
		bool containsInvalid = false;
		Regex containsInvalidRegex = new Regex("^[a-zA-Z0-9_]*$");
		
		bool isFirstNumber = false;
		Regex isNumberRegex = new Regex("^[0-9]*$");
		
		bool isEmptyString = false;
		
		for(int i = 0; i < buffer.Count; i++)
		{
			var define = buffer[i];
			
			if(define.name.Length == 0)
			{
				isEmptyString = true;
			}
			
			if(!containsInvalidRegex.IsMatch(define.name))
			{
				containsInvalid = true;
			}
			
			if(define.name.Length != 0)
			{
				if(isNumberRegex.IsMatch(define.name[0].ToString()))
				{
					isFirstNumber = true;
				}
			}
		}
		
		errors.Clear();
		
		if(containsInvalid)
		{
			errors.Add("A script define contains invalid characters.");
		}
		
		if(isEmptyString)
		{
			errors.Add("A script define is empty.");
		}
		
		if(isFirstNumber)
		{
			errors.Add("A number cannot be used as the first character of a define.");
		}
	}
	
	void Apply ()
	{
		foreach(BuildTargetGroup type in Enum.GetValues(typeof(BuildTargetGroup)))
		{
			StringBuilder builder = new StringBuilder();
			foreach(var define in buffer)
			{
				if(!define.isEnabled)
				{
					builder.Append("*");
				}
				if(!define.groups.Contains(type))
				{
					builder.Append("|");
				}
				builder.Append(define.name);
				builder.Append(";");
			}
			PlayerSettings.SetScriptingDefineSymbolsForGroup((BuildTargetGroup)type, builder.ToString());
		}
	}
}
