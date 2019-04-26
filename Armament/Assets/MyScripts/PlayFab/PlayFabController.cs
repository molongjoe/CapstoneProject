﻿using System.Collections.Generic;
using Photon.Pun;
using PlayFab;
using PlayFab.ClientModels;
using PlayFab.Json;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;

namespace Com.Kabaj.TestPhotonMultiplayerFPSGame
{
    public class PlayFabController : MonoBehaviour
    {
        public static PlayFabController PFC;

        private string userEmail;
        private string userPassword;
        private string username;
        private string displayName;
        private string sharedGroupId;
        private List<FriendInfo> _friends;
        private List<string> _friendStringIDs;
        private List<string> _sharedGroupMembers;
        public string sharedGroupRoom;
        private bool registering = false;
        private CameraAnimationHandler cameraMover;
        private SceneManager SM;
        public GameObject loginAndRegisterPanel;
        public GameObject mainMenuPanel;
        public GameObject optionsPanel;
        public GameObject matchmakingPanel;
        public GameObject friendsPanel;
        public GameObject messagesPanel;
        public GameObject loginOptionsPanel;
        public GameObject registerOptionsPanel;
        public GameObject newUserButton;
        public GameObject OpenLoginAgain;
        public GameObject progressLabel;

        //convenience variables
        private EventSystem system;
        public bool autoLogin;

        private void OnEnable()
        {
            if (PlayFabController.PFC == null)
            {
                PlayFabController.PFC = this;
            }
            else
            {
                if (PlayFabController.PFC != this)
                {
                    Destroy(this.gameObject);
                }
            }
           //DontDestroyOnLoad(this.gameObject);
        }

        public void Start()
        {
            //First deactivate other panels and gray out the register section
            Defocus(mainMenuPanel, false);
            Defocus(optionsPanel, false);
            Defocus(matchmakingPanel, false);
            Defocus(friendsPanel, false);
            Defocus(messagesPanel, false);
            Defocus(registerOptionsPanel, true);

            //Prepare Camera Moving component
            cameraMover = GameObject.Find("Main Camera").GetComponent<CameraAnimationHandler>();

            //If auto login is not enabled
            if (!autoLogin)
            {
                PlayerPrefs.DeleteAll();
            }

            //Note: Setting title Id here can be skipped if you have set the value in Editor Extensions already.
            if (string.IsNullOrEmpty(PlayFabSettings.TitleId))
            {
                PlayFabSettings.TitleId = "E5D9";
            }

            //If player has signed in before, automatically assign their credentials 
            if (PlayerPrefs.HasKey("EMAIL"))
            {
                userEmail = PlayerPrefs.GetString("EMAIL");
                userPassword = PlayerPrefs.GetString("PASSWORD");
                var request = new LoginWithEmailAddressRequest { Email = userEmail, Password = userPassword };
                PlayFabClientAPI.LoginWithEmailAddress(request, OnEmailLoginSuccess, OnEmailLoginFailure);
            }

            else if (PlayerPrefs.HasKey("USERNAME"))
            {
                username = PlayerPrefs.GetString("USERNAME");
                userPassword = PlayerPrefs.GetString("PASSWORD");
                var request = new LoginWithPlayFabRequest { Username = username, Password = userPassword };
                PlayFabClientAPI.LoginWithPlayFab(request, OnPlayfabLoginSuccess, OnPlayfabLoginFailure);
            }

            else
            {
#if UNITY_ANDROID
            var requestAndroid = new LoginWithAndroidDeviceIDRequest { AndroidDeviceId = returnMobileID(), CreateAccount = true };
            PlayFabClientAPI.LoginWithAndroidDeviceID(requestAndroid, OnLoginMobileSuccess, OnLoginMobileFailure);
            progressLabel.GetComponent<Text>().text = "Logging in with AndroidDeviceID";
#endif
#if UNITY_IOS
            var requestIOS = new LoginWithIOSDeviceIDRequest { DeviceId = returnMobileID(), CreateAccount = true };
            PlayFabClientAPI.LoginWithIOSDeviceID(requestIOS, OnLoginMobileSuccess, OnLoginMobileFailure);
            progressLabel.GetComponent<Text>().text = "Logging in with AppleDeviceID";
#endif
            }

            _friends = new List<FriendInfo>();
            _friendStringIDs = new List<string>();
            _sharedGroupMembers = new List<string>();

            system = EventSystem.current;
        }

        // Update is called once per frame
        void Update()
        {
            //Tab through input fields
            if (Input.GetKeyDown(KeyCode.Tab))
            {
                Selectable next = system.currentSelectedGameObject.GetComponent<Selectable>().FindSelectableOnDown();

                if (next != null)
                {
                    InputField inputfield = next.GetComponent<InputField>();
                    if (inputfield != null)
                        inputfield.OnPointerClick(new PointerEventData(system));  //if it's an input field, also set the text caret

                    system.SetSelectedGameObject(next.gameObject, new BaseEventData(system));
                }
                else Debug.Log("next nagivation element not found");
            }
            else if (Input.GetKeyDown(KeyCode.Return))
            {
                OnClickLogin();
            }
        }

        void OnSceneWasSwitched()
        {
            username = "wtfwtf";
            userPassword = "wtfwtf";
            OnClickLogin();
        }
        #region Login

        //Login section
        public void OnClickLogin()
        {
            if (username != null || userPassword != null)
            {

                //Submit a login request to the server
                if (username.EndsWith(".com") || username.EndsWith(".org") || username.EndsWith(".edu") || username.EndsWith(".gov"))// trying to cover all cases... maybe we should be searching for existence of an "@" symbol and a subsequent "." within the string to generalize it more
                {
                    Debug.Log("Logging in with Email Address/Password");
                    progressLabel.GetComponent<Text>().text = "Logging in with Email Address/Password";
                    var request = new LoginWithEmailAddressRequest { Email = username, Password = userPassword };
                    PlayFabClientAPI.LoginWithEmailAddress(request, OnEmailLoginSuccess, OnEmailLoginFailure);
                }

                else
                {
                    Debug.Log("Logging in with Username/Password");
                    progressLabel.GetComponent<Text>().text = "Logging in with Username/Password";
                    var request = new LoginWithPlayFabRequest { Username = username, Password = userPassword };
                    PlayFabClientAPI.LoginWithPlayFab(request, OnPlayfabLoginSuccess, OnPlayfabLoginFailure);
                }
            }
            else
                progressLabel.GetComponent<Text>().text = "Please enter your username/email and password";
        }

        private void OnEmailLoginSuccess(LoginResult result)
        {
            //Log and set player's Email and Password strings for next time
            Debug.Log("Login Success for user with email " + username);
            progressLabel.GetComponent<Text>().text = "Login Success for user with email " + username;
            PlayerPrefs.SetString("EMAIL", username);
            PlayerPrefs.SetString("PASSWORD", userPassword);

            //set the shared group ID to be the creator's PlayFabID + the Title ID
            sharedGroupId = result.PlayFabId + "_" + PlayFabSettings.TitleId;

            //Get user's friends
            GetFriends();

            GetSharedGroupData();

            //Bring Login and register panel out of focus and bring in Main Menu
            Defocus(loginAndRegisterPanel, false);
            Refocus(mainMenuPanel, false);

            //Move the camera to reveal database and launch options
            cameraMover.MoveLeft();
        }

        private void OnEmailLoginFailure(PlayFabError error)
        {
            Debug.Log(error.GenerateErrorReport());
            string errorString = error.GenerateErrorReport().Replace("/Client/LoginWithPlayFab", "Error");
            //Debug.Log("ErrorString: " + errorString);
            progressLabel.GetComponent<Text>().text = errorString;
        }

        private void OnPlayfabLoginSuccess(LoginResult result)
        {
            //Log and set player's Email and Password strings for next time
            Debug.Log("Login Success for User \"" + username);
            progressLabel.GetComponent<Text>().text = "Login Success for user " + username;
            PlayerPrefs.SetString("USERNAME", username);
            PlayerPrefs.SetString("PASSWORD", userPassword);

            //set the shared group ID to be the creator's PlayFabID + the Title ID
            sharedGroupId = result.PlayFabId + "_" + PlayFabSettings.TitleId;

            //Get user's friends
            GetFriends();

            GetSharedGroupData();

            //Bring Login and register panel out of focus and bring in Main Menu
            Defocus(loginAndRegisterPanel, false);
            Refocus(mainMenuPanel, false);

            //Move the camera to reveal database and launch options
            cameraMover.MoveLeft();
        }

        private void OnPlayfabLoginFailure(PlayFabError error)
        {
            //Debug.Log(error.GenerateErrorReport());
            string errorString = error.GenerateErrorReport().Replace("/Client/LoginWithPlayFab", "Error");
            //Debug.Log("ErrorString: " + errorString);
            progressLabel.GetComponent<Text>().text = errorString;
        }

        private void OnLoginMobileSuccess(LoginResult result)
        {
            Debug.Log("Mobile Login Success for User \"" + username);
            progressLabel.GetComponent<Text>().text = "Mobile Login Success for user " + username;

            //set the shared group ID to be the creator's PlayFabID + the Title ID
            sharedGroupId = result.PlayFabId + "_" + PlayFabSettings.TitleId;

            //Bring Login and register panel out of focus and bring in Main Menu
            Defocus(loginAndRegisterPanel, false);
            Refocus(mainMenuPanel, false);

            //Move the camera to reveal the new input options
            cameraMover.MoveLeft();
        }

        private void OnLoginMobileFailure(PlayFabError error)
        {
            Debug.Log(error.GenerateErrorReport());
            string errorString = error.GenerateErrorReport().Replace("/Client/LoginWithPlayFab", "Error");
            //Debug.Log("ErrorString: " + errorString);
            progressLabel.GetComponent<Text>().text = errorString;
        }

        public void OnClickOpenLoginAgain()
        {
            //Log and set player's Email and Password strings for next time
            Debug.Log("Back to login panel");

            foreach (Transform child in loginOptionsPanel.transform)
            {
                if (child.gameObject.tag == "InputField")
                {
                    child.GetComponent<InputField>().text = "";
                }
            }
            foreach (Transform child in registerOptionsPanel.transform)
            {
                if (child.gameObject.tag == "InputField")
                {
                    child.GetComponent<InputField>().text = "";
                }
            }
            //Bring Main Menu out of focus and bring Login/Register Panel
            Defocus(mainMenuPanel, false);
            Refocus(loginAndRegisterPanel, false);

            //Move the camera to reveal database and launch options
            cameraMover.MoveRight();
        }

        //Register section
        public void OnClickRegister()
        {
            var registerRequest = new RegisterPlayFabUserRequest { Email = userEmail, Password = userPassword, Username = username, DisplayName = displayName };
            PlayFabClientAPI.RegisterPlayFabUser(registerRequest, OnRegisterSuccess, OnRegisterFailure);
        }

        private void OnRegisterSuccess(RegisterPlayFabUserResult result)
        {
            //Log and set player's Email and Password strings for next time
            Debug.Log("Registration Success for User \"" + username);
            progressLabel.GetComponent<Text>().text = "Registration Success for user " + username;
            PlayerPrefs.SetString("USERNAME", username);
            PlayerPrefs.SetString("EMAIL", userEmail);
            PlayerPrefs.SetString("PASSWORD", userPassword);

            //set the shared group ID to be the creator's PlayFabID + the Title ID
            sharedGroupId = result.PlayFabId + "_" + PlayFabSettings.TitleId;

            //Create what will amount to be a "friend group"
            CreateSharedGroup(sharedGroupId);

            //user is not registering anymore
            registering = false;

            //Bring Login and register panel out of focus and bring in Main Menu
            Defocus(loginAndRegisterPanel, false);
            Refocus(mainMenuPanel, false);

            //Move the camera to reveal the new input options
            cameraMover.MoveLeft();
        }

        private void OnRegisterFailure(PlayFabError error)
        {
            Debug.LogError(error.GenerateErrorReport());
            string errorString = error.GenerateErrorReport().Replace("/Client/LoginWithPlayFab", "Error");
            //Debug.Log("ErrorString: " + errorString);
            progressLabel.GetComponent<Text>().text = errorString;
        }

        void OnDisplayName(UpdateUserTitleDisplayNameResult result)
        {
            Debug.Log(result.DisplayName + " is your new display name");
            progressLabel.GetComponent<Text>().text = result.DisplayName + " is your new display name";
        }

        //Setters
        public void SetUserEmail(string emailIn)
        {
            userEmail = emailIn;
        }

        public void SetUserPassword(string passwordIn)
        {
            userPassword = passwordIn;
        }

        public void SetUsername(string usernameIn)
        {
            username = usernameIn;
        }

        public void SetDisplayName(string displayNameIn)
        {
            displayName = displayNameIn;
        }

        private static string returnMobileID()
        {
            string deviceID = SystemInfo.deviceUniqueIdentifier;
            return deviceID;
        }

        public void OnClickNewUser()
        {
            if (!registering)
            {
                Defocus(loginOptionsPanel, true);
                Refocus(registerOptionsPanel, true);
                registering = true;
                newUserButton.GetComponentInChildren<Text>().text = "Already have an account?";

            }
            else
            {
                Defocus(registerOptionsPanel, true);
                Refocus(loginOptionsPanel, true);
                registering = false;
                newUserButton.GetComponentInChildren<Text>().text = "New User?";
            }
        }

        private void Defocus(GameObject GO, bool dampenColor)
        {
            foreach (Transform child in GO.transform)
            {
                if (child.GetComponent<Image>() && dampenColor)
                {
                    var tempColor = child.GetComponent<Image>().color;
                    tempColor.a = 0.5f;
                    child.GetComponent<Image>().color = tempColor;
                }
                if (child.gameObject.tag == "InputField")
                {
                    child.GetComponent<InputField>().enabled = false;
                }
                if (child.gameObject.tag == "Button")
                {
                    child.GetComponent<Button>().enabled = false;
                }
            }
        }

        private void Refocus(GameObject GO, bool restoreColor)
        {
            foreach (Transform child in GO.transform)
            {
                if (child.GetComponent<Image>() && restoreColor)
                {
                    var tempColor = child.GetComponent<Image>().color;
                    tempColor.a = 1;
                    child.GetComponent<Image>().color = tempColor;
                }
                if (child.gameObject.tag == "InputField")
                {
                    child.GetComponent<InputField>().enabled = true;
                }
                if (child.gameObject.tag == "Button")
                {
                    child.GetComponent<Button>().enabled = true;
                }
            }
        }

        #endregion Login

        #region PlayerStats

        public int playerKillCount;

        //Receive stats from database
        void GetStats()
        {
            PlayFabClientAPI.GetPlayerStatistics(
                new GetPlayerStatisticsRequest(),
                OnGetStatistics,
                error => Debug.LogError(error.GenerateErrorReport())
            );
        }

        //Log each stat to the user
        void OnGetStatistics(GetPlayerStatisticsResult result)
        {
            Debug.Log("Received the following Statistics:");
            foreach (var eachStat in result.Statistics)
            {
                Debug.Log("Statistic (" + eachStat.StatisticName + "): " + eachStat.Value);
                switch (eachStat.StatisticName)
                {
                    case "PlayerKillCount":
                        playerKillCount = eachStat.Value;
                        break;
                }
            }
        }

        // Build the request object and access the API to update player stats
        public void StartCloudUpdatePlayerStats()
        {
            PlayFabClientAPI.ExecuteCloudScript(new ExecuteCloudScriptRequest()
            {
                FunctionName = "UpdatePlayerStats", // Arbitrary function name (must exist in the uploaded cloud.js file)
                FunctionParameter = new { pKillCount = playerKillCount }, // The parameter provided to your function
                GeneratePlayStreamEvent = true, // Optional - Shows this event in PlayStream
            }, OnCloudUpdateStats, OnErrorShared);
        }


        //Code to run when an update stats call is successful
        private static void OnCloudUpdateStats(ExecuteCloudScriptResult result)
        {
            // Cloud Script returns arbitrary results, so you have to evaluate them one step and one parameter at a time
            Debug.Log(JsonWrapper.SerializeObject(result.FunctionResult));
            JsonObject jsonResult = (JsonObject)result.FunctionResult;
            object messageValue;
            jsonResult.TryGetValue("messageValue", out messageValue); // note how "messageValue" directly corresponds to the JSON values set in Cloud Script
            Debug.Log((string)messageValue);
        }

        private static void OnErrorShared(PlayFabError error)
        {
            Debug.Log(error.GenerateErrorReport());
        }

        #endregion PlayerStats

        #region Leaderboard

        public GameObject leaderboardPanel;
        public GameObject listingPrefab;
        public Transform listingContainer;

        public void GetLeaderboard()
        {
            var requestLeaderboard = new GetLeaderboardRequest { StartPosition = 0, StatisticName = "PlayerKillCount", MaxResultsCount = 20 };
            PlayFabClientAPI.GetLeaderboard(requestLeaderboard, OnGetLeaderboard, OnErrorLeaderboard);
        }

        void OnGetLeaderboard(GetLeaderboardResult result)
        {
            leaderboardPanel.SetActive(true);
            foreach (PlayerLeaderboardEntry player in result.Leaderboard)
            {
                GameObject tempListing = Instantiate(listingPrefab, listingContainer);
                LeaderboardListing LL = tempListing.GetComponent<LeaderboardListing>();
                LL.playerNameText.text = player.DisplayName;
                LL.playerScoreText.text = player.StatValue.ToString();
                Debug.Log(player.DisplayName + ": " + player.StatValue);
            }
        }

        public void CloseLeaderboardPanel()
        {
            leaderboardPanel.SetActive(false);
            for (int i = listingContainer.childCount - 1; i >= 0; i--)
            {
                Destroy(listingContainer.GetChild(i).gameObject);
            }
        }

        void OnErrorLeaderboard(PlayFabError error)
        {
            Debug.LogError(error.GenerateErrorReport());
        }

        #endregion Leaderboard

        #region Friends

        string friendUsername;

        enum FriendIdType { PlayFabId, Username, Email, DisplayName };

        void AddFriend(FriendIdType idType, string friendId)
        {
            var request = new AddFriendRequest();
            switch (idType)
            {
                case FriendIdType.PlayFabId:
                    request.FriendPlayFabId = friendId;
                    break;
                case FriendIdType.Username:
                    request.FriendUsername = friendId;
                    break;
                case FriendIdType.Email:
                    request.FriendEmail = friendId;
                    break;
                case FriendIdType.DisplayName:
                    request.FriendTitleDisplayName = friendId;
                    break;
            }
            // Execute request and update friends when we are done
            PlayFabClientAPI.AddFriend(request, result =>
            {
                Debug.Log("Friend added successfully!");
            }, DisplayPlayFabError);
        }

        public void GetFriends()
        {
            PlayFabClientAPI.GetFriendsList(new GetFriendsListRequest
            {
                IncludeSteamFriends = false,
                IncludeFacebookFriends = false
            }, result =>
            {
                _friends = result.Friends;
                DisplayFriends(_friends); // triggers the UI
            }, DisplayPlayFabError);
        }

        void DisplayFriends(List<FriendInfo> friendsCache) { friendsCache.ForEach(f => Debug.Log(f.Username)); }
        void DisplayPlayFabError(PlayFabError error) { Debug.Log(error.GenerateErrorReport()); }
        void DisplayError(string error) { Debug.LogError(error); }

        public void OnClickAddFriend()
        {
            AddFriend(FriendIdType.Username, friendUsername);
        }

        public void SetFriendUsername(string value)
        {
            friendUsername = value;
        }

        //This is a test method
        public void OnClickShowFriendsAndLeaderboard()
        {
            //Log and set player's Email and Password strings for next time
            Debug.Log("Going to Friends/Leaderboards panel");

            //Bring Main Menu out of focus and bring in Options Panel
            Defocus(mainMenuPanel, false);
            Refocus(optionsPanel, false);

            //Move the camera to reveal database and launch options
            cameraMover.MoveBack();
        }

        public void CreateSharedGroup(string Id)
        {
            PlayFabClientAPI.CreateSharedGroup(new CreateSharedGroupRequest
            {
                SharedGroupId = Id
            }, OnCreateSharedGroup, OnErrorCreateSharedGroup);
        }

        public void OnCreateSharedGroup(CreateSharedGroupResult result)
        {
            Debug.Log("Created shared group with ID \"" + result.SharedGroupId + "\"");
        }

        public void OnErrorCreateSharedGroup(PlayFabError error)
        {
            Debug.LogError(error.GenerateErrorReport());
        }

        public void OnClickAddSharedGroupMembers()
        {
            //This call populates the _friends List
            GetFriends();

            Debug.Log(_friends.Count);

            //Populate _friendStringIDs as a list from friend list
            FriendsToStringList();

            if (_friendStringIDs.Count != 0)
            {
                //Add all friends to shared group
                PlayFabClientAPI.AddSharedGroupMembers(new AddSharedGroupMembersRequest
                {
                    SharedGroupId = sharedGroupId,
                    PlayFabIds = _friendStringIDs
                }, OnAddSharedGroupMembers, OnErrorAddSharedGroupMembers);
            }

            else
            {
                GetSharedGroupData();
            }
        }

        public void OnAddSharedGroupMembers(AddSharedGroupMembersResult result)
        {
            //Get the Data after adding members to the group
            GetSharedGroupData();
        }

        public void OnErrorAddSharedGroupMembers(PlayFabError error)
        {
            Debug.LogError(error.GenerateErrorReport());
        }

        //Convert friends in friends list to string ID's and return
        public void FriendsToStringList()
        {
            foreach (FriendInfo f in _friends)
            {
                foreach (string member in _sharedGroupMembers)
                {
                    //if user isn't already added to the shared group
                    if (member != f.FriendPlayFabId)
                    {
                        Debug.Log("Adding Friend String from " + f.FriendPlayFabId);
                        _friendStringIDs.Add(f.FriendPlayFabId);
                    }
                }
            }
        }

        public void GetSharedGroupData()
        {
            PlayFabClientAPI.GetSharedGroupData(new GetSharedGroupDataRequest
            {
                SharedGroupId = sharedGroupId,
                GetMembers = true,
            }, OnGetSharedGroupData, OnErrorGetSharedGroupData);
        }

        public void OnGetSharedGroupData(GetSharedGroupDataResult result)
        {
            //Debug shared group data to the user
            Debug.Log("\nSHARED GROUP\n" +
                "ID: " + sharedGroupId + "\nMembers");
            foreach (string member in result.Members)
            {
                Debug.Log(member);
            }

            if (result.Data.ContainsKey("roomName"))
            {
                foreach (KeyValuePair<string, SharedGroupDataRecord> item in result.Data)
                {
                    Debug.Log("Key: " + item.Key + " | Value: " + item.Value.Value);
                    sharedGroupRoom = item.Value.Value;
                }

            }

            _sharedGroupMembers = result.Members;
        }

        public void OnErrorGetSharedGroupData(PlayFabError error)
        {
            Debug.LogError(error.GenerateErrorReport());
        }

        public void OnClickSetPrivateRoomName()
        {
            PlayFabClientAPI.UpdateSharedGroupData(new UpdateSharedGroupDataRequest()
            {
                SharedGroupId = sharedGroupId,
                Data = new Dictionary<string, string>() {
                {"roomName", sharedGroupRoom}
            },
                Permission = PlayFab.ClientModels.UserDataPermission.Public
            }, OnUpdateSharedGroupData, OnErrorUpdateSharedGroupData);
        }

        public void OnUpdateSharedGroupData(UpdateSharedGroupDataResult result)
        {
            GetSharedGroupData();
        }

        public void OnErrorUpdateSharedGroupData(PlayFabError error)
        {
            Debug.LogError(error.GenerateErrorReport());
        }

        public void SetSharedGroupRoom(string sharedGroupRoomIn)
        {
            sharedGroupRoom = sharedGroupRoomIn;
        }

        public void SetSharedGroupID(string groupIDIn)
        {
            sharedGroupId = groupIDIn;
        }

        #endregion Friends
    }
}
