using System.IO;
using UdonSharpEditor;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace Synergiance.MediaPlayer {
	[CustomEditor(typeof(PlaylistManager))]
	public class PlaylistManagerEditor : Editor {
		private SerializedProperty playlistsProp;
		private ReorderableList playlistNamesList;

		private SerializedProperty playlistProp;
		private SerializedProperty videosProp;
		private ReorderableList videoList;
		private SerializedProperty videoProp;
		private SerializedProperty linksProp;
		private ReorderableList linkList;
		private SerializedProperty linkProp;

		private SerializedProperty backupProp;

		private SerializedProperty debugProp;
		private SerializedProperty diagnosticsProp;

		private bool hasStagedChanges;
		private bool needsFullRebuild;

		private void OnEnable() {
			debugProp = serializedObject.FindProperty("debug");
			diagnosticsProp = serializedObject.FindProperty("diagnostics");
			playlistsProp = serializedObject.FindProperty("playlistData").FindPropertyRelative("playlists");
			backupProp = serializedObject.FindProperty("playlistBackup");
			playlistNamesList = new ReorderableList(serializedObject, playlistsProp, true, true, true, true) {
				drawHeaderCallback = DrawPlaylistsHeader,
				drawElementCallback = DrawPlaylistNames,
				onSelectCallback = SelectPlaylist,
				onAddCallback = AddPlaylist,
				onRemoveCallback = RemovePlaylist,
				onChangedCallback = ListChanged
			};
			SelectPlaylist(playlistNamesList);
		}

		public override void OnInspectorGUI() {
			if (UdonSharpGUI.DrawDefaultUdonSharpBehaviourHeader(target)) return;
			serializedObject.Update();
			EditorGUI.BeginChangeCheck();
			EditorGUILayout.PropertyField(debugProp);
			EditorGUILayout.PropertyField(diagnosticsProp);
			EditorGUILayout.Space();
			EditorGUILayout.PropertyField(backupProp);
			if (EditorGUI.EndChangeCheck()) serializedObject.ApplyModifiedProperties();
			DrawSaveLoadButtons();
			playlistNamesList.DoLayoutList();
			RenderPlaylistDetails();
			FinalizeChanges();
		}

		private void RenderPlaylistDetails() {
			if (playlistNamesList.index < 0) return;
			EditorGUILayout.Space();
			EditorGUI.BeginChangeCheck();
			EditorGUILayout.PropertyField(playlistProp.FindPropertyRelative("name"));
			if (EditorGUI.EndChangeCheck()) ApplyChanges();
			videoList?.DoLayoutList();
			RenderVideoDetails();
		}

		private void RenderVideoDetails() {
			if (videoList == null) return;
			if (videoList.index < 0) return;
			EditorGUILayout.Space();
			EditorGUI.BeginChangeCheck();
			EditorGUILayout.PropertyField(videoProp.FindPropertyRelative("name"));
			EditorGUILayout.PropertyField(videoProp.FindPropertyRelative("shortName"));
			if (EditorGUI.EndChangeCheck()) ApplyChanges();
			linkList?.DoLayoutList();
			RenderLinkDetails();
		}

		private void RenderLinkDetails() {
			if (linkList == null) return;
			if (linkList.index < 0) return;
			EditorGUILayout.Space();
			EditorGUI.BeginChangeCheck();
			EditorGUILayout.PropertyField(linkProp.FindPropertyRelative("type"));
			EditorGUILayout.PropertyField(linkProp.FindPropertyRelative("pc"));
			EditorGUILayout.PropertyField(linkProp.FindPropertyRelative("quest"));
			if (EditorGUI.EndChangeCheck()) ApplyChanges();
		}

		// ReSharper disable Unity.PerformanceAnalysis
		private void DrawSaveLoadButtons() {
			Rect ctlRect = EditorGUILayout.GetControlRect();
			float buttonsWidth = ctlRect.width / 2;
			Rect loadRect = new Rect(ctlRect.x, ctlRect.y, buttonsWidth, ctlRect.height);
			Rect saveRect = new Rect(ctlRect.x + buttonsWidth, ctlRect.y, buttonsWidth, ctlRect.height);
			if (GUI.Button(loadRect, "Load")) LoadPlaylists();
			if (GUI.Button(saveRect, "Save")) SavePlaylists();
		}

		private void AddPlaylist(ReorderableList _list) {
			int index = _list.serializedProperty.arraySize;
			_list.serializedProperty.arraySize++;
			_list.index = index;
			SerializedProperty newElement = _list.serializedProperty.GetArrayElementAtIndex(index);
			newElement.FindPropertyRelative("name").stringValue = "New Playlist";
			newElement.FindPropertyRelative("videos").arraySize = 0;
			SelectPlaylist(_list);
		}

		private void RemovePlaylist(ReorderableList _list) {
			int index = _list.index;
			_list.index = -1;
			playlistProp = null;
			videosProp = null;
			videoList = null;
			videoProp = null;
			linksProp = null;
			linkList = null;
			linkProp = null;
			_list.serializedProperty.DeleteArrayElementAtIndex(index);
		}

		private void SelectPlaylist(ReorderableList _list) {
			if (_list.index < 0) return;
			playlistProp = playlistsProp.GetArrayElementAtIndex(_list.index);
			videosProp = playlistProp.FindPropertyRelative("videos");
			videoList = new ReorderableList(serializedObject, videosProp, true, true, true, true) {
				drawHeaderCallback = DrawVideosHeader,
				drawElementCallback = DrawVideoNames,
				onAddCallback = AddVideo,
				onRemoveCallback = RemoveVideo,
				onSelectCallback = SelectVideo,
				onChangedCallback = ListChanged
			};
		}

		private void DrawPlaylistsHeader(Rect _rect) {
			EditorGUI.LabelField(_rect, "Playlists");
		}

		private void DrawPlaylistNames(Rect _rect, int _index, bool _isActive, bool _isFocused) {
			SerializedProperty element = playlistNamesList.serializedProperty.GetArrayElementAtIndex(_index);
			SerializedProperty nameProperty = element.FindPropertyRelative("name");
			EditorGUI.LabelField(_rect, nameProperty.stringValue);
		}

		private void AddVideo(ReorderableList _list) {
			int index = _list.serializedProperty.arraySize;
			_list.serializedProperty.arraySize++;
			_list.index = index;
			SerializedProperty newElement = _list.serializedProperty.GetArrayElementAtIndex(index);
			newElement.FindPropertyRelative("name").stringValue = "New Video";
			newElement.FindPropertyRelative("shortName").stringValue = "New Video";
			newElement.FindPropertyRelative("links").arraySize = 0;
			SelectVideo(_list);
			AddLink(linkList);
		}

		private void RemoveVideo(ReorderableList _list) {
			int index = _list.index;
			_list.index = -1;
			videoProp = null;
			linksProp = null;
			linkList = null;
			linkProp = null;
			_list.serializedProperty.DeleteArrayElementAtIndex(index);
		}

		private void SelectVideo(ReorderableList _list) {
			if (_list.index < 0) return;
			videoProp = videosProp.GetArrayElementAtIndex(_list.index);
			linksProp = videoProp.FindPropertyRelative("links");
			linkList = new ReorderableList(serializedObject, linksProp, true, true, true, true) {
				drawHeaderCallback = DrawLinksHeader,
				drawElementCallback = DrawLinkTypes,
				onAddCallback = AddLink,
				onRemoveCallback = RemoveLink,
				onSelectCallback = SelectLink,
				onChangedCallback = ListChanged
			};
		}

		private void DrawVideosHeader(Rect _rect) {
			EditorGUI.LabelField(_rect, "Videos");
		}

		private void DrawVideoNames(Rect _rect, int _index, bool _isActive, bool _isFocused) {
			SerializedProperty element = videoList.serializedProperty.GetArrayElementAtIndex(_index);
			SerializedProperty nameProperty = element.FindPropertyRelative("name");
			EditorGUI.LabelField(_rect, nameProperty.stringValue);
		}

		private void AddLink(ReorderableList _list) {
			int index = _list.serializedProperty.arraySize;
			_list.serializedProperty.arraySize++;
			_list.index = index;
			SerializedProperty newElement = _list.serializedProperty.GetArrayElementAtIndex(index);
			newElement.FindPropertyRelative("type").stringValue = "link" + (index + 1);
			newElement.FindPropertyRelative("pc").stringValue = "";
			newElement.FindPropertyRelative("quest").stringValue = "";
			SelectLink(_list);
		}

		private void RemoveLink(ReorderableList _list) {
			int index = _list.index;
			linkProp = null;
			_list.serializedProperty.DeleteArrayElementAtIndex(index);
			if (_list.count < 1) AddLink(_list);
			_list.index = -1;
		}

		private void SelectLink(ReorderableList _list) {
			if (_list.index < 0) return;
			linkProp = linksProp.GetArrayElementAtIndex(_list.index);
		}

		private void DrawLinksHeader(Rect _rect) {
			EditorGUI.LabelField(_rect, "Links");
		}

		private void DrawLinkTypes(Rect _rect, int _index, bool _isActive, bool _isFocused) {
			SerializedProperty element = linkList.serializedProperty.GetArrayElementAtIndex(_index);
			SerializedProperty nameProperty = element.FindPropertyRelative("type");
			EditorGUI.LabelField(_rect, nameProperty.stringValue);
		}

		private void ListChanged(ReorderableList _list) {
			//Debug.Log("Change: " + _list.serializedProperty.name);
			ApplyChanges();
			needsFullRebuild = true;
		}

		private void ApplyChanges() {
			serializedObject.ApplyModifiedProperties();
			hasStagedChanges = true;
		}

		private void FinalizeChanges() {
			if (!hasStagedChanges) return;
			((PlaylistManager)target).RebuildSerialized(needsFullRebuild);
			serializedObject.Update();
			hasStagedChanges = false;
			needsFullRebuild = false;
		}

		private void SavePlaylists() {
			((PlaylistManager)target).SaveToJson(CalculatePlaylistPath() + ".dat");
		}

		private void LoadPlaylists() {
			Undo.RecordObject(target, "Loaded new playlists from backup");
			string playlistPath = CalculatePlaylistPath();
			if (!File.Exists(playlistPath + ".dat")) ConvertPlaylists();
			else ((PlaylistManager)target).LoadFromJson(CalculatePlaylistPath() + ".dat");
			PrefabUtility.RecordPrefabInstancePropertyModifications(target);
			playlistNamesList.index = -1;
			playlistProp = null;
			videosProp = null;
			videoList = null;
			videoProp = null;
			linksProp = null;
			linkList = null;
			linkProp = null;
		}

		private void ConvertPlaylists() {
			string playlistPath = CalculatePlaylistPath();
			PlaylistManager playlistManager = (PlaylistManager)target;
			if (playlistManager.LoadFrom(playlistPath))
				playlistManager.SaveToJson(playlistPath + ".dat");
		}

		private string CalculatePlaylistPath() {
			return Application.persistentDataPath + "/SMPBackups/" + backupProp.stringValue;
		}
	}
}
