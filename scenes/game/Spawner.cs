using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class Spawner : Node
{
	[Export] public PackedScene TrainScene;
	[Export] public Godot.Collections.Array<Marker2D> LeftSpawnPoints;
	[Export] public Godot.Collections.Array<Marker2D> RightSpawnPoints;

	// ★距離設定（ピクセル単位）
	[Export] public float StandardGap = 2160.0f;   // 通常の車間距離
	[Export] public float ConnectedGap = 360.0f;   // 2両編成に見せるための短い距離

	// ★ポーリング間隔
	// 距離チェックを頻繁に行うため、タイマーは短く回し続ける
	[Export] public float CheckInterval = 0.1f; 

	private bool _isAOnScreen = false;
	private bool _isBOnScreen = false;
	
	// ★各レーンで「最後に出現させた電車」を記憶しておく配列
	private Train[] _lastSpawnedTrains;
	
	// ★各レーンで「次に空けるべき距離」を記憶しておく配列
	private float[] _requiredGaps; 

	private Random _random = new Random();
	private Timer _spawnTimer;

	public override void _Ready()
	{
		_spawnTimer = GetNode<Timer>("Timer");
		_spawnTimer.Timeout += OnCheckSpawnLoop; // 名前変更: SpawnTimer -> CheckSpawnLoop
		_spawnTimer.WaitTime = CheckInterval;
		// 常に監視し続けるのでループさせる
		_spawnTimer.OneShot = false; 
		_spawnTimer.Start();

		if (LeftSpawnPoints != null)
		{
			int count = LeftSpawnPoints.Count;
			_lastSpawnedTrains = new Train[count];
			_requiredGaps = new float[count];

			// 初期状態はすべてのレーンでスポーン可能にするため、ダミーの距離0を設定
			for(int i=0; i<count; i++) _requiredGaps[i] = 0f; 
		}
	}

	// ★定期的（0.1秒ごと）に呼ばれ、空いているレーンがないかチェックする
	private void OnCheckSpawnLoop()
	{
		if (GameScene.Instance.IsGameOver) return;
		if (TrainScene == null || LeftSpawnPoints.Count == 0) return;

		// 1. スポーン可能な（前の電車が十分離れた）レーンをリストアップ
		List<int> availableLanes = GetAvailableLanes();

		if (availableLanes.Count == 0) return;

		// 2. 空きレーンからランダムに1つ選ぶ
		int chosenLaneIndex = availableLanes[_random.Next(availableLanes.Count)];

		// 3. タイプ決定
		TrainType nextType = DecideNextType();

		// 4. 2両編成チャンス判定（例えば20%の確率で次は連結にする）
		bool isConnected = _random.NextDouble() < 0.0; 
		
		// 5. 生成実行
		SpawnTrain(chosenLaneIndex, nextType, isConnected);
	}

	private List<int> GetAvailableLanes()
	{
		List<int> list = new List<int>();
		for (int i = 0; i < _lastSpawnedTrains.Length; i++)
		{
			Train lastTrain = _lastSpawnedTrains[i];
			float neededGap = _requiredGaps[i];

			// まだ一度も出していない or 前の電車が消滅している -> OK
			if (lastTrain == null || !IsInstanceValid(lastTrain))
			{
				list.Add(i);
				continue;
			}

			// 前の電車との距離を測る
			// 注意: 左レーンと右レーンで進行方向が違うので、単純な座標差ではなく距離(Distance)を見る
			// ここでは簡略化のため、SpawnPointからの絶対距離で判定
			float dist = 0f;
			
			// 該当レーンのスポーン地点座標を取得
			// (左右どちらから出たかに関わらず、スポーン地点からどれだけ離れたかを見ればよい)
			// ※簡略化のため「現在のX座標」と「出現したMarkerのX座標」の差分絶対値を見る
			Vector2 spawnPos = (lastTrain.MoveDirection == Vector2.Right) 
								? LeftSpawnPoints[i].Position 
								: RightSpawnPoints[i].Position;

			dist = Math.Abs(lastTrain.Position.X - spawnPos.X);

			// 必要な距離以上離れていれば、次のスポーンOK
			if (dist >= neededGap)
			{
				list.Add(i);
			}
		}
		return list;
	}

	private void SpawnTrain(int laneIndex, TrainType type, bool startConnectionChain)
	{
		// 左右決定（このレーンで現在動いている方向があればそれに合わせるべきだが、
		// 今回は毎回ランダム、あるいは「前の電車が消えていればランダム」とする）
		// ※2両編成にするなら、前の電車と同じ方向である必要があります。
		
		Vector2 direction;
		Vector2 spawnPos;
		bool isLeftStart;

		// 直前の電車が存在し、かつ画面内にまだ残っているなら、同じ方向から出す（追いかける形）
		Train lastTrain = _lastSpawnedTrains[laneIndex];
		if (lastTrain != null && IsInstanceValid(lastTrain))
		{
			direction = lastTrain.MoveDirection;
			spawnPos = (direction == Vector2.Right) ? LeftSpawnPoints[laneIndex].Position : RightSpawnPoints[laneIndex].Position;
		}
		else
		{
			// 前がいないならランダム
			isLeftStart = _random.Next(2) == 0;
			if (isLeftStart)
			{
				spawnPos = LeftSpawnPoints[laneIndex].Position;
				direction = Vector2.Right;
			}
			else
			{
				spawnPos = RightSpawnPoints[laneIndex].Position;
				direction = Vector2.Left;
			}
		}

		// 生成
		var train = TrainScene.Instantiate<Train>();
		GetParent().AddChild(train);
		train.Position = spawnPos;
		train.Initialize(type, direction);
		
		// 状態管理
		RegisterActiveState(type);
		train.TreeExited += () => UnregisterActiveState(type);

		// ★記録更新
		_lastSpawnedTrains[laneIndex] = train;

		// ★次回のスポーン条件を設定
		if (startConnectionChain)
		{
			// 「次は連結だ！」→ 短い距離でOKにする
			_requiredGaps[laneIndex] = ConnectedGap;
		}
		else
		{
			// 「次は通常だ」→ 通常距離離れるまで待つ
			_requiredGaps[laneIndex] = StandardGap;
		}
	}

	// (DecideNextType, RegisterActiveState, UnregisterActiveState は変更なし)
	private TrainType DecideNextType()
	{
		var candidates = new List<TrainType>();
		candidates.Add(TrainType.Dummy);
		candidates.Add(TrainType.Dummy);
		if (!_isAOnScreen) candidates.Add(TrainType.CharA);
		if (!_isBOnScreen) candidates.Add(TrainType.CharB);
		if (!_isAOnScreen && !_isBOnScreen) candidates.Add(TrainType.Mixed);
		return candidates[_random.Next(candidates.Count)];
	}

	private void RegisterActiveState(TrainType type)
	{
		switch (type) { case TrainType.CharA: _isAOnScreen = true; break; case TrainType.CharB: _isBOnScreen = true; break; case TrainType.Mixed: _isAOnScreen = true; _isBOnScreen = true; break; }
	}

	private void UnregisterActiveState(TrainType type)
	{
		switch (type) { case TrainType.CharA: _isAOnScreen = false; break; case TrainType.CharB: _isBOnScreen = false; break; case TrainType.Mixed: _isAOnScreen = false; _isBOnScreen = false; break; }
	}
}
