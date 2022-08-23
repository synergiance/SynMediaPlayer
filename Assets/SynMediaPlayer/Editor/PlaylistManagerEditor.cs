using System.Runtime.CompilerServices;
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

		private SerializedProperty backupProp;

		private SerializedProperty debugProp;
		private SerializedProperty diagnosticsProp;

		private bool hasStagedChanges;
		private bool needsFullRebuild;

		private void OnEnable() {
			debugProp = serializedObject.FindProperty("debug");
			diagnosticsProp = serializedObject.FindProperty("diagnostics");
			playlistsProp = serializedObject.FindProperty("playlists");
			backupProp = serializedObject.FindProperty("playlistBackup");
			playlistNamesList = new ReorderableList(serializedObject, playlistsProp, true, true, true, true);
			playlistNamesList.drawHeaderCallback = DrawPlaylistsHeader;
			playlistNamesList.drawElementCallback = DrawPlaylistNames;
			playlistNamesList.onSelectCallback = SelectPlaylist;
			playlistNamesList.onAddCallback = AddPlaylist;
			playlistNamesList.onRemoveCallback = RemovePlaylist;
			playlistNamesList.onChangedCallback = ListChanged;
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
			EditorGUILayout.PropertyField(videoProp.FindPropertyRelative("link"));
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
			_list.serializedProperty.DeleteArrayElementAtIndex(index);
		}

		private void SelectPlaylist(ReorderableList _list) {
			if (_list.index < 0) return;
			playlistProp = playlistsProp.GetArrayElementAtIndex(_list.index);
			videosProp = playlistProp.FindPropertyRelative("videos");
			videoList = new ReorderableList(serializedObject, videosProp, true, true, true, true);
			videoList.drawHeaderCallback = DrawVideosHeader;
			videoList.drawElementCallback = DrawVideoNames;
			videoList.onAddCallback = AddVideo;
			videoList.onRemoveCallback = RemoveVideo;
			videoList.onSelectCallback = SelectVideo;
			videoList.onChangedCallback = ListChanged;
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
			//newElement.FindPropertyRelative("link").DeleteCommand();
			SelectVideo(_list);
		}

		private void RemoveVideo(ReorderableList _list) {
			int index = _list.index;
			_list.index = -1;
			_list.serializedProperty.DeleteArrayElementAtIndex(index);
		}

		private void SelectVideo(ReorderableList _list) {
			if (_list.index < 0) return;
			videoProp = videosProp.GetArrayElementAtIndex(_list.index);
		}

		private void DrawVideosHeader(Rect _rect) {
			EditorGUI.LabelField(_rect, "Videos");
		}

		private void DrawVideoNames(Rect _rect, int _index, bool _isActive, bool _isFocused) {
			SerializedProperty element = videoList.serializedProperty.GetArrayElementAtIndex(_index);
			SerializedProperty nameProperty = element.FindPropertyRelative("name");
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
			((PlaylistManager)target).SaveTo(CalculatePlaylistPath());
		}

		private void LoadPlaylists() {
			((PlaylistManager)target).LoadFrom(CalculatePlaylistPath());
		}

		private string CalculatePlaylistPath() {
			// TODO: Implement
			return "";
		}
	}
}
