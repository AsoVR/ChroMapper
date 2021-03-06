﻿using CustomFloorPlugin;
using System;
using System.Collections;
using System.IO;
using UnityEditor;
using UnityEngine;

public class LoadInitialMap : MonoBehaviour {

    [SerializeField] AudioTimeSyncController atsc;
    [SerializeField] RotationCallbackController rotationController;
    [Space]
    [SerializeField] NotesContainer notesContainer;
    [SerializeField] ObstaclesContainer obstaclesContainer;
    [SerializeField] MapLoader loader;
    [Space]
    [SerializeField] GameObject[] PlatformPrefabs;
    [SerializeField] GameObject[] DirectionalPlatformPrefabs;
    [SerializeField] GameObject[] CustomPlatformPrefabs;

    public static Action<PlatformDescriptor> PlatformLoadedEvent;
    public static Action LevelLoadedEvent;
    public static readonly Vector3 PlatformOffset = new Vector3(0, -0.5f, -1.5f);

    private BeatSaberSong song;
    private BeatSaberSong.DifficultyBeatmap diff;
    private int totalObjectsToLoad = 0;
    private int totalObjectsLoaded = 0;

    void Awake()
    {
        SceneTransitionManager.Instance.AddLoadRoutine(LoadMap());
    }

    public IEnumerator LoadMap()
    {
        if (BeatSaberSongContainer.Instance == null) yield break;
        PersistentUI.Instance.LevelLoadSliderLabel.text = "";
        yield return new WaitUntil(() => atsc.gridStartPosition != -1); //I need a way to find out when Start has been called.

        song = BeatSaberSongContainer.Instance.song; //Grab songe data
        diff = BeatSaberSongContainer.Instance.difficultyData;

        //Set up some local variables
        int environmentID = 0;
        bool customPlat = false;
        bool directional = false;

        environmentID = SongInfoEditUI.GetEnvironmentIDFromString(song.environmentName); //Grab platform by name (Official or Custom)
        if (song.customData != null && song.customData["_customEnvironment"] != null && song.customData["_customEnvironment"].Value != "")
        {
            if (CustomPlatformsLoader.Instance.GetAllEnvironmentIds().IndexOf(song.customData["_customEnvironment"] ?? "") >= 0) {
                customPlat = true;
            }
        }
        if (rotationController.IsActive && diff.parentBeatmapSet.beatmapCharacteristicName != "Lawless")
        {
            environmentID = SongInfoEditUI.GetDirectionalEnvironmentIDFromString(song.allDirectionsEnvironmentName);
            customPlat = false;
            directional = true;
        }

        //Instantiate platform, grab descriptor
        GameObject platform = (customPlat ? CustomPlatformsLoader.Instance.LoadPlatform(song.customData["_customEnvironment"], (PlatformPrefabs[environmentID]) ?? PlatformPrefabs[0], null) : PlatformPrefabs[environmentID]) ?? PlatformPrefabs[0];
        if (directional) platform = DirectionalPlatformPrefabs[environmentID];
        GameObject instantiate = null;
        if (customPlat)
        {
            instantiate = platform;
        }
        else
        {
            Debug.Log("Instanciate nonCustomPlat");
            instantiate = Instantiate(platform, PlatformOffset, Quaternion.identity) as GameObject;
        }
        PlatformDescriptor descriptor = instantiate.GetComponent<PlatformDescriptor>();
        BeatmapEventContainer.ModifyTypeMode = descriptor.SortMode; //Change sort mode

        //Update Colors
        Color leftNote = BeatSaberSong.DEFAULT_LEFTNOTE; //Have default note as base
        if (descriptor.RedColor != BeatSaberSong.DEFAULT_LEFTCOLOR) leftNote = descriptor.RedColor; //Prioritize platforms
        if (diff.colorLeft != null) leftNote = diff.colorLeft ?? leftNote; //Then prioritize custom colors

        Color rightNote = BeatSaberSong.DEFAULT_RIGHTNOTE;
        if (descriptor.BlueColor != BeatSaberSong.DEFAULT_RIGHTCOLOR) rightNote = descriptor.BlueColor;
        if (diff.colorRight != null) rightNote = diff.colorRight ?? rightNote;

        notesContainer.UpdateColor(leftNote, rightNote);
        obstaclesContainer.UpdateColor(diff.obstacleColor ?? BeatSaberSong.DEFAULT_LEFTCOLOR);
        if (diff.colorLeft != null) descriptor.RedNoteColor = diff.colorLeft ?? descriptor.RedNoteColor;
        if (diff.colorRight != null) descriptor.BlueNoteColor = diff.colorRight ?? descriptor.BlueNoteColor;

        //We set light color to envColorLeft if it exists. If it does not exist, but colorLeft does, we use colorLeft.
        //If neither, we use default platform lights.
        if (diff.envColorLeft != null) descriptor.RedColor = diff.envColorLeft ?? descriptor.RedColor;
        else if (diff.colorLeft != null) descriptor.RedColor = diff.colorLeft ?? descriptor.RedColor;

        //Same thing for envColorRight
        if (diff.envColorRight != null) descriptor.BlueColor = diff.envColorRight ?? descriptor.BlueColor;
        else if (diff.colorRight != null) descriptor.BlueColor = diff.colorRight ?? descriptor.BlueColor;

        PlatformLoadedEvent.Invoke(descriptor); //Trigger event for classes that use the platform

        loader.UpdateMapData(BeatSaberSongContainer.Instance.map);
        yield return StartCoroutine(loader.HardRefresh());
        LevelLoadedEvent?.Invoke();
    }
}
