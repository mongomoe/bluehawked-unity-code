﻿using UnityEngine;
using Realms;
using System.Threading.Tasks;
using Realms.Sync;
using UnityEngine.SceneManagement;
using MongoDB.Bson;
using System.Linq;
public class RealmController : MonoBehaviour
{
    public static RealmController Instance;

    public Stat currentStat; // the Stat object for the current playthrough
    public User syncUser; // (Part 2 Sync): syncUser represents the realmApp's currently logged in use

    private Realm realm;
    private int runTime; // total amount of time you've been playing during this playthrough/run (losing/winning resets runtime)
    private int bonusPoints = 0; // start with 0 bonus points and at the end of the game we add bonus points based on how long you played
    private Player currentPlayer; // the Player object for the current playthrough
    private App realmApp = App.Create(Constants.Realm.AppId); // (Part 2 Sync): realmApp represents the MongoDB Realm backend application

    // setLoggedInUser() is an asynchronous method that logs in as a Realms.Sync.User, creates a new Stat object for the current playthrough
    // and returns the Player object that corresponds to the logged in Realms.Sync.User
    // setLoggedInUser() takes a userInput and passInput, representing a username/password, as a parameter
    public async Task<Player> SetLoggedInUser(string userInput, string passInput)
    {
        syncUser = await realmApp.LogInAsync(Credentials.EmailPassword(userInput, passInput));
        if (syncUser != null)
        {
            realm = await GetRealm(syncUser);
            currentPlayer = realm.Find<Player>(syncUser.Id);
            if (currentPlayer != null)
            {
                var s1 = new Stat();
                s1.StatOwner = currentPlayer;
                realm.Write(() =>
                {
                    currentStat = realm.Add(s1);
                    currentPlayer.Stats.Add(currentStat);
                });
                StartGame();
            }
            else
            {
                Debug.Log("This player exists a MongoDB Realm User but not as a Realm Object, please delete the MongoDB Realm User and create one using the Game rather than MongoDB Atlas or Realm Studio");
            }
        }
        return currentPlayer;
    }

    // OnPressRegister() is an asynchronous method that registers as a Realms.Sync.User, creates a new Player and Stat object 
    // OnPressRegister takes a userInput and passInput, representing a username/password, as a parameter
    public async Task<Player> OnPressRegister(string userInput, string passInput)
    {
        await realmApp.EmailPasswordAuth.RegisterUserAsync(userInput, passInput);
        syncUser = await realmApp.LogInAsync(Credentials.EmailPassword(userInput, passInput));
        realm = await GetRealm(syncUser);

        var p1 = new Player();
        p1.Id = syncUser.Id;
        p1.Name = userInput;
        var s1 = new Stat();
        s1.StatOwner = p1;
        realm.Write(() =>
        {
            currentPlayer = realm.Add(p1);
            currentStat = realm.Add(s1);
            currentPlayer.Stats.Add(currentStat);
        });
        StartGame();
        return currentPlayer;
    }

    // LogOut() is an asynchronous method that logs out and reloads the scene
    public async void LogOut()
    {
        await syncUser.LogOutAsync();
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    // collectToken() is a method that performs a write transaction to update the current playthrough Stat object's TokensCollected count
    public void CollectToken()
    {
        realm.Write(() =>
        {
            currentStat.TokensCollected += 1;
        });
    }
    // defeatEnemy() is a method that performs a write transaction to update the current playthrough Stat object's enemiesDefeated count
    public void DefeatEnemy()
    {
        realm.Write(() =>
        {
            currentStat.EnemiesDefeated += 1;
        });
    }

    // deleteCurrentStat() is a method that performs a write transaction to delete the current playthrough Stat object and remove it from the current Player object's Stats' list
    public void DeleteCurrentStat()
    {
        FindObjectOfType<ScoreCardManager>().UnRegisterListener();
        realm.Write(() =>
        {
            realm.Remove(currentStat);
            currentPlayer.Stats.Remove(currentStat);
        });
    }
    // restartGame() is a method that creates a new plathrough Stat object and shares this new Stat object with the ScoreCardManager to update in the UI and listen for changes to it
    public void RestartGame()
    {
        var s1 = new Stat();
        s1.StatOwner = currentPlayer;
        realm.Write(() =>
        {
            currentStat = realm.Add(s1);
            currentPlayer.Stats.Add(currentStat);
        });

        FindObjectOfType<ScoreCardManager>().SetCurrentStat(currentStat); // call `SetCurrentStat()` to set the current stat in the UI using ScoreCardManager
        FindObjectOfType<ScoreCardManager>().WatchForChangesToCurrentStats(); // call `WatchForChangesToCurrentStats()` to register a listener on the new score in the ScoreCardManager

        StartGame(); // start the game by resetting the timer and officially starting a new run/playthrough
    }


    // playerWon() is a method that calculates and returns the final score for the current playthrough once the player has won the game
    public int PlayerWon()
    {
        if (runTime <= 30) // if the game is beat in in less than or equal to 30 seconds, +80 bonus points
        {
            bonusPoints = 80;
        }
        else if (runTime <= 60) // if the game is beat in in less than or equal to 1 min, +70 bonus points
        {
            bonusPoints = 70;
        }
        else if (runTime <= 90) // if the game is beat in less than or equal to 1 min 30 seconds, +60 bonus points
        {
            bonusPoints = 60;
        }
        else if (runTime <= 120) // if the game is beat in less than or equal to 2 mins, +50 bonus points
        {
            bonusPoints = 50;
        }

        var finalScore = (currentStat.EnemiesDefeated + 1) * (currentStat.TokensCollected + 1) + bonusPoints;
        realm.Write(() =>
        {
            currentStat.Score = finalScore;
        });

        return finalScore;
    }

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        // Attach C# Scripts to UI GameObjects
        var authScreen = GameObject.Find("AuthenticationScreen");
        authScreen.AddComponent<AuthenticationManager>();

        var leaderboard = GameObject.Find("Leaderboard");
        leaderboard.AddComponent<LeaderboardManager>();

        var scorecard = GameObject.Find("Scorecard");
        scorecard.AddComponent<ScoreCardManager>();

    }

    // GetRealm() is an asynchronous method that returns a synced realm
    // GetRealm() takes a logged in Realms.Sync.User as a parameter
    private async Task<Realm> GetRealm(User loggedInUser)
    {
        var syncConfiguration = new SyncConfiguration("UnityTutorialPartition", loggedInUser);
        return await Realm.GetInstanceAsync(syncConfiguration);
    }

    // startGame() is a method that records how long the player has been playing during the current playthrough (i.e since logging in or since last losing or winning)
    private void StartGame()
    {
        // execute a timer every 10 second
        var myTimer = new System.Timers.Timer(10000);
        myTimer.Enabled = true;
        myTimer.Elapsed += (sender, e) => runTime += 10; // increment runTime (runTime will be used to calculate bonus points once the player wins the game)
    }
    
}
