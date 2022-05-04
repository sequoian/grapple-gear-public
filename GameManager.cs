using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;

public class GameManager : MonoBehaviour
{
    List<string> levels;
    int currentRoomIndex;
    int nextRoomIndex;

    void OnEnable()
    {
        SetLevelOrder();

        // Check if the user is loading into a room directly
        Scene activeScene = SceneManager.GetActiveScene();
        if (Helpers.GetSceneRoot(activeScene.name).GetComponent<Room>() != null)
        {
            currentRoomIndex = levels.IndexOf(activeScene.name);
            StartCoroutine(LoadRoomDirectlyCoroutine());
        }

        // Hide mouse
        Cursor.visible = false;
    }

    void Update()
    {
        if (InputManager.GetKeyDown(UnityEngine.InputSystem.Key.Equals) &&
            Helpers.GetSceneRoot(SceneManager.GetActiveScene().name).tag == "Room")
        {
            LoadNextRoom();
        }
    }

    public void StartGame()
    {
        // Load into the Main Menu from boot
        StartCoroutine(StartGameCoroutine());
    }

    IEnumerator StartGameCoroutine()
    {
        // Load settings menu
        yield return LoadSceneAdditive("Settings Menu");

        // Load main menu
        yield return LoadSceneAdditive("Main Menu");

        // Fade out loading screen
        LoadingScreen loadingScreen = Helpers.GetSceneRoot(
            "Loading screen").GetComponent<LoadingScreen>();
        yield return loadingScreen.FadeOut();
    }

    public void EndGame()
    {
        StartCoroutine(EndGameCoroutine());
    }

    IEnumerator EndGameCoroutine()
    {
        // Fade in loading screen
        LoadingScreen loadingScreen = Helpers.GetSceneRoot(
            "Loading screen").GetComponent<LoadingScreen>();
        yield return loadingScreen.FadeIn();

        

        Application.Quit();
    }

    public void LoadRooms(int roomIndex)
    {
        // Load from Main Menu into the levels
        StartCoroutine(LoadRoomsCoroutine(roomIndex));
    }

    IEnumerator LoadRoomsCoroutine(int roomIndex)
    {
        if (roomIndex >= levels.Count || roomIndex < 0)
        {
            // Loop rooms
            roomIndex = 0;
        }

        string roomName = levels[roomIndex];

        // Show loading screen and disable main menu
        LoadingScreen loadingScreen = Helpers.GetSceneRoot(
            "Loading Screen").GetComponent<LoadingScreen>();
        yield return loadingScreen.FadeIn();
        Helpers.GetSceneRoot("Main Menu").SetActive(false);

        // Load game UI
        yield return LoadSceneAdditive("Level Complete Menu");
        yield return LoadSceneAdditive("Pause Menu");

        // Load room
        yield return LoadSceneAdditive(roomName);
        SceneManager.SetActiveScene(SceneManager.GetSceneByName(roomName));
        currentRoomIndex = roomIndex;

        // Hide loading screen
        yield return loadingScreen.FadeOut();

        // Unload main menu
        yield return SceneManager.UnloadSceneAsync("Main Menu");

        // Load next room
        yield return LoadFollowingRoom();
    }

    public void LoadMainMenu()
    {
        // Load into the Main Menu from the levels
        StartCoroutine(LoadMainMenuCoroutine());
    }

    IEnumerator LoadMainMenuCoroutine()
    {
        string currentRoomName = SceneManager.GetActiveScene().name;

        // Show loading screen
        LoadingScreen loadingScreen = Helpers.GetSceneRoot(
            "Loading Screen").GetComponent<LoadingScreen>();
        yield return loadingScreen.FadeIn();

        // Disable room and game UI
        Helpers.GetSceneRoot(currentRoomName).SetActive(false);
        Helpers.GetSceneRoot("Level Complete Menu").SetActive(false);
        Helpers.GetSceneRoot("Pause Menu").SetActive(false);

        // Load main menu
        yield return LoadSceneAdditive("Main Menu");
        SceneManager.SetActiveScene(SceneManager.GetSceneByName("Main Menu"));

        // Hide loading screen
        yield return loadingScreen.FadeOut();

        // Unload current room
        yield return SceneManager.UnloadSceneAsync(currentRoomName);

        // Unload next room if it's loaded
        if (nextRoomIndex < levels.Count && 
            SceneManager.GetSceneByName(levels[nextRoomIndex]).IsValid())
        {
            yield return SceneManager.UnloadSceneAsync(levels[nextRoomIndex]);
        }

        // Unload room UI
        yield return SceneManager.UnloadSceneAsync("Level Complete Menu");
        yield return SceneManager.UnloadSceneAsync("Pause Menu");
    }

    public void LoadNextRoom()
    {
        // Load from room to room
        StartCoroutine(LoadNextRoomCoroutine());
    }

    IEnumerator LoadNextRoomCoroutine()
    {
        if (nextRoomIndex >= levels.Count || nextRoomIndex < 0)
        {
            yield return LoadMainMenuCoroutine();
            yield break;
        }

        string prevRoomName = levels[currentRoomIndex];
        string roomName = levels[nextRoomIndex];
        Scene currentScene = SceneManager.GetActiveScene();

        // Disable current room and enable next room
        Helpers.GetSceneRoot(currentScene.name).SetActive(false);
        Helpers.GetSceneRoot(roomName).SetActive(true);
        SceneManager.SetActiveScene(SceneManager.GetSceneByName(roomName));
        currentRoomIndex = nextRoomIndex;

        // Unload previous room
        yield return SceneManager.UnloadSceneAsync(prevRoomName);

        // Load the following room
        yield return LoadFollowingRoom();
    }

    IEnumerator LoadRoomDirectlyCoroutine()
    {
        // Load settings menu
        yield return LoadSceneAdditive("Settings Menu");

        // Load game UI
        yield return LoadSceneAdditive("Level Complete Menu");
        yield return LoadSceneAdditive("Pause Menu");

        // Load loading screen
        yield return LoadSceneAdditive("Loading Screen");

        // Immediately disable camera
        Helpers.GetSceneRoot("Loading Screen").GetComponent<LoadingScreen>().Disable();

        if (currentRoomIndex < 0)
        {
            // Room is not a part of the order, so set it to loop
            Helpers.GetSceneRoot(
                SceneManager.GetActiveScene().name).GetComponent<Room>().loopRoom = true;
        }
        else
        {
            // Load next room
            yield return LoadFollowingRoom();
        }
    }

    IEnumerator LoadSceneAdditive(string sceneName)
    {
        if (!SceneManager.GetSceneByName(sceneName).isLoaded)
        {
            yield return SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
        }
        else
        {
            yield return null;
        }
    }

    IEnumerator LoadFollowingRoom()
    {
        nextRoomIndex = currentRoomIndex + 1;
        if (nextRoomIndex >= levels.Count || nextRoomIndex < 0)
        {
            // Do not load a room if it is out of range
            yield break;
        }

        // Load next room
        string nextRoomName = levels[nextRoomIndex];
        yield return LoadSceneAdditive(nextRoomName);

        // Immediately disable the room
        Helpers.GetSceneRoot(nextRoomName).SetActive(false);

        yield return null;
    }

    void SetLevelOrder()
    {
        // levels = new List<string>
        // {
        //     "Tutorial 1",
        //     "Intro 1",
        //     "Intro 14 (2)",
        //     "Intro 9 (2)",
        //     "Intro 3 (2)",
        //     "Intro 15",
        //     "Intro 12",
        //     "Intro 4 (2)",
        //     "Intro 10",
        //     "Intro 5"
        // };

        levels = new List<string>
        {
            // "Jump 1",
            "Jump 2",
            // "Jump 3",
            // "Crumble 1",
            "Wall 1",
            "Crumble 2",
            "Wall 2",
            "Swing 1",
            // "Swing 2",
            "Swing 3",
            // "Geo 1",
            "Wall 3",
            "Wall 4",
            // "Crumble 3",
            "Crumble 4",
            "Geo 2",
            "Spikes 1",
            "Spikes 2",
            "Electric 1",
            "Electric 2",
            // "Wall 5",
            "Wall 6"
        };
    }
}
