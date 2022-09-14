using TwinCAT;
using TwinCAT.Ads;
using TwinCAT.Ads.TcpRouter;
using System.Net;

namespace DataCollectionService
{
    public sealed class AdsDataCollection
    {
        #region Public Vars
        public Dictionary<string, string> DataList = new();
        public bool OnConnection = false;
        public AdsErrorCode _adsErrorCode = AdsErrorCode.NoError;
        #endregion

        #region Local Vars

        private AdsState _adsState = AdsState.Init;
        private ConnectionState _connectionStatus = ConnectionState.None;

        private readonly AdsSession Session;
        private readonly AmsNetId AmsNetID;
        private readonly AdsClient PlcAdsClient = new();

        public AdsDataCollection(IConfiguration configuration)
        {
            var netID = configuration.GetValue<string>("AmsNetId",AmsNetId.Local.ToString());

            AmsNetID = new(string.IsNullOrEmpty(netID) ?AmsNetId.Local.ToString(): netID);
            Session = new(AmsNetID, 10000, SessionSettings.FastWriteThrough);

            DictionaryInitialize(configuration, DataList);

            _connectionStatus = SessionConnect();

        }
        #endregion

        public async Task Ads(CancellationToken stoppingToken)
        {
            OnConnection = false;

            if (Session.Connection != null && _connectionStatus == ConnectionState.Connected)
            {
                _adsState = ReadAdsState(Session.Connection);

                if (_adsState == AdsState.Run)
                {
                    //PLC RUNTIME
                    if (ServerControl(PlcAdsClient, new AmsAddress(AmsNetID, AmsPort.PlcRuntime_851),out _adsErrorCode))
                    {
                        OnConnection = true;
                        await DictionaryUpdate(DataList, PlcAdsClient,  stoppingToken);
                    }
                    // 
                }
                else
                {
                    if (PlcAdsClient.IsConnected) { PlcAdsClient.Disconnect(); }
                    _adsErrorCode = AdsRun(Session.Connection);
                }
            }
            else
            {
                if (PlcAdsClient.IsConnected) { PlcAdsClient.Disconnect(); }
                IPConnect();
            }

            await Task.Delay(1000, stoppingToken);
        }

        #region Other Methods

        private void Session_ConnectionStateChanged(object? sender, ConnectionStateChangedEventArgs e)
        {
            _connectionStatus = e.NewState;
        }
        public static void DictionaryInitialize(IConfiguration configuration,Dictionary<string, string> dic)
        {
            dic.Add("GVL.ST_Dat1.bVar1", "0");
            dic.Add("GVL.ST_Dat1.bVar2", "0");
            dic.Add("GVL.ST_Dat1.fVar3", "0");
            dic.Add("GVL.ST_Dat1.fVar4", "0");
            dic.Add("GVL.ST_Dat1.nVar5", "0");
            dic.Add("GVL.ST_Dat1.nVar6", "0");
            dic.Add("GVL.ST_Dat1.nVar7.fVar1", "0");
            dic.Add("GVL.ST_Dat1.nVar7.fVar2", "0");
            dic.Add("GVL.ST_Dat1.nVar7.nVar3", "0");

            uint k = 0;
            while (true)
            {
                string? Key = configuration.GetValue<string>($"Keys:{k}");
                k++;

                if (Key is null)
                { 
                    return; 
                }
                else if (Key == string.Empty) 
                { 
                    ; 
                }
                else
                {
                    dic.Add(Key, string.Empty);
                }
            }            
        }
        private static async Task DictionaryUpdate(Dictionary<string, string> dic, AdsClient client, CancellationToken token)
        {
            foreach (KeyValuePair<string, string> kvp in dic)
            {
                dic[kvp.Key] = await ReadValue(kvp.Key, client, token) ?? "0";
            }
        }
        private static bool ServerControl(AdsClient client, AmsAddress amsAddress,out AdsErrorCode ErrorCode)
        {
            ErrorCode = AdsErrorCode.NoError;

            bool serverRun = false;

            if (client.IsConnected)
            {
                if (ReadAdsState(client) == AdsState.Run)
                {
                    serverRun = true;
                }
                else
                {
                    ErrorCode = AdsRun(client);
                }
            }
            else
            {
                client.Connect(amsAddress);
            }
            return serverRun;
        }
        private static async Task<string?> ReadValue(string key, AdsClient client, CancellationToken token)
        {
            ResultValue<string> result = await client.ReadValueAsync<string>(key, token);

            return result.Succeeded ? result.Value : "0";
        }

        #endregion

        #region AdsRun Methods
        private static AdsErrorCode AdsRun(AdsConnection connection)
        {
            return (ReadAdsState(connection) == AdsState.Stop) ? connection.TryWriteControl(new StateInfo(AdsState.Start, 0)) : connection.TryWriteControl(new StateInfo(AdsState.Reset, 0));
        }
        private static AdsErrorCode AdsRun(AdsClient client)
        {
            return (ReadAdsState(client) == AdsState.Stop) ? client.TryWriteControl(new StateInfo(AdsState.Start, 0)) : client.TryWriteControl(new StateInfo(AdsState.Reset, 0));
        }
        #endregion

        #region AdsStatusRead Methods
        private static AdsState ReadAdsState(AdsConnection connection)
        {
            var state = AdsState.Error;

            if (connection.TryReadState(out StateInfo stateInfo) == AdsErrorCode.NoError)
            {
                state = stateInfo.AdsState;
            }

            return state;
        }
        private static AdsState ReadAdsState(AdsClient client)
        {
            var state = AdsState.Error;

            if (client.TryReadState(out StateInfo stateInfo) == AdsErrorCode.NoError)
            {
                state = stateInfo.AdsState;
            }

            return state;
        }
        #endregion

        #region Connection Methods
      
        private ConnectionState SessionConnect()
        {
            var state = ConnectionState.None;

            try
            {
                state = Session.Connect().ConnectionState;
                Session.ConnectionStateChanged += Session_ConnectionStateChanged;
            }
            catch (Exception ex)
            {
                _= "Connection Error: " + ex.Message;
            }

            return state;
        }

        private void SessionDisconnect()
        {
            try
            {
                Session.Disconnect();
                Session.ConnectionStateChanged -= Session_ConnectionStateChanged;
            }
            catch (Exception ex)
            {
                _ = ex.Message;
            }
            
        }

        private void IPConnect()
        {
            switch (_connectionStatus)
            {
                case ConnectionState.None | ConnectionState.Disconnected:
                    _connectionStatus = SessionConnect();
                    break;

                case ConnectionState.Lost:
                    SessionDisconnect();
                    break;
            }
        }
        #endregion

        public void Dispose()
        {
            SessionDisconnect();
            Session.Dispose();

            PlcAdsClient.Disconnect();
            PlcAdsClient.Dispose();
        }
    }
}
