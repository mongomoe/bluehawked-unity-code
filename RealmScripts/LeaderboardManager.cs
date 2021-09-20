using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Realms;
using System.Linq;
using Realms.Sync;
using System.Threading.Tasks;
public class LeaderboardManager : MonoBehaviour
{
    public static LeaderboardManager Instance;
    private Realm realm;
    private VisualElement root;
    private ListView listView;
    private Label displayTitle;
    private string username;
    private bool isLeaderboardUICreated = false;
    private List<Stat> topStats;
    private IDisposable listenerToken;  // (Part 2 Sync): listenerToken is the token for registering a change listener on all Stat objects
    void Awake()
    {
        Instance = this;
    }
    // GetRealm() is an asynchronous method that returns a synced realm
    private static async Task<Realm> GetRealm()
    {
        var syncConfiguration = new SyncConfiguration("UnityTutorialPartition", RealmController.syncUser);
        return await Realm.GetInstanceAsync(syncConfiguration);
    }
    // setLoggedInUser() is an asynchronous method that opens a realm, calls the createLeaderboardUI() method to create the LeaderboardUI and adds it to the Root Component
    // and calls setStatListener() to start listening for changes to all Stat objects in order to update the global leaderboard
    // setLoggedInUser()  takes a userInput, representing a username, as a parameter
    public async void setLoggedInUser(string userInput)
    {
        username = userInput;

        realm = await GetRealm();

        // only create the leaderboard on the first run, consecutive restarts/reruns will already have a leaderboard created
        if (isLeaderboardUICreated == false)
        {
            root = GetComponent<UIDocument>().rootVisualElement;
            createLeaderboardUI();
            root.Add(displayTitle);
            root.Add(listView);
            isLeaderboardUICreated = true;
        }
        setStatListener();
    }
    // getRealmPlayerTopStat() is a method that queries a realm for the player's Stat object with the highest score
    private int getRealmPlayerTopStat()
    {
        var realmPlayer = realm.All<Player>().Where(p => p.Name == username).First();
        var realmPlayerTopStat = realmPlayer.Stats.OrderByDescending(s => s.Score).First().Score;
        return realmPlayer.Stats.OrderByDescending(s => s.Score).First().Score;
    }
    // createLeaderboardUI() is a method that creates a Leaderboard title for
    // the UI and calls createTopStatListView() to create a list of Stat objects
    // with high scores
    private void createLeaderboardUI()
    {
        // create leaderboard title
        displayTitle = new Label();
        displayTitle.text = "Leaderboard:";
        displayTitle.AddToClassList("display-title");

        topStats = realm.All<Stat>().OrderByDescending(s => s.Score).ToList();
        createTopStatListView();
    }
    // createTopStatListView() is a method that creates a set of Labels containing high stats
    private void createTopStatListView()
    {
        // set the maximumAmountOfTopStats to 5 or less
        if (topStats.Count > 4)
        {
            maximumAmountOfTopStats = 5;
        }
        else
        {
            maximumAmountOfTopStats = topStats.Count;
        }


        var topStatsListItems = new List<string>();

        topStatsListItems.Add("Your top points: " + getRealmPlayerTopStat());


        for (int i = 0; i < maximumAmountOfTopStats; i++)
        {
            if (topStats[i].Score > 1) // only display the top stats if they are greater than 0, and show no top stats if there are none greater than 0
            {
                topStatsListItems.Add($"{topStats[i].StatOwner.Name}: {topStats[i].Score} points");
            }
        };
        // Create a new label for each top score
        var label = new Label();
        label.AddToClassList("list-item-game-name-label");
        Func<VisualElement> makeItem = () => new Label();

        // Bind Stats to the UI
        Action<VisualElement, int> bindItem = (e, i) =>
        {
            (e as Label).text = topStatsListItems[i];
            (e as Label).AddToClassList("list-item-game-name-label");
        };

        // Provide the list view with an explict height for every row
        // so it can calculate how many items to actually display
        const int itemHeight = 5;

        listView = new ListView(topStatsListItems, itemHeight, makeItem, bindItem);
        listView.AddToClassList("list-view");

    }
    // setStatListener is a method that sets a listener on all Stat objects, and calls setNewlyInsertedScores if one has been inserted
    private void setStatListener()
    {

        // Observe collection notifications. Retain the token to keep observing.
        listenerToken = realm.All<Stat>()
            .SubscribeForNotifications((sender, changes, error) =>
            {

                if (error != null)
                {
                    // Show error message
                    Debug.Log("an error occurred while listening for score changes :" + error);
                    return;
                }

                if (changes != null)
                {
                    setNewlyInsertedScores(changes.InsertedIndices);
                }
                // we only need to check for inserted Stat objects because Stat objects can't be modified or deleted after the playthrough is complete

            });
    }

    // setNewlyInsertedScores() is a method that determine if a new Stat is greater than any existing topStats, and if it is, inserts it into the topStats list in descending order
    // setNewlyInsertedScores() takes an array of insertedIndices
    private void setNewlyInsertedScores(int[] insertedIndices)
    {
        foreach (var i in insertedIndices)
        {
            var newStat = realm.All<Stat>().ElementAt(i);

            for (var scoreIndex = 0; scoreIndex < topStats.Count; scoreIndex++)
            {
                if (topStats.ElementAt(scoreIndex).Score < newStat.Score)
                {
                    if (topStats.Count > 4)
                    {   // An item shouldnt be removed if its the leaderboard is less than 5 items
                        topStats.RemoveAt(topStats.Count - 1);
                    }
                    topStats.Insert(scoreIndex, newStat);
                    root.Remove(listView); // remove the old listView
                    createTopStatListView(); // create a new listView
                    root.Add(listView); // add the new listView to the UI
                    break;
                }
            }
        }
    }
    void OnDisable()
    {
        if (listenerToken != null)
        {
            listenerToken.Dispose();
        }
    }
}