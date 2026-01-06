using Godot;
using System;

public partial class GameScene : Node2D // ルートノードがNode2Dなので合わせる
{
	// ★シングルトンインスタンス: 他のスクリプトから GameScene.Instance でアクセス可能にする
	public static GameScene Instance { get; private set; }

	[Export] public float InitialTime = 30.0f;

	// UIへの参照
	private Label _scoreLabel;
	private Label _timeLabel;
	private Label _gameOverLabel;

	// ゲーム内変数
	public int Score { get; private set; } = 0;
	public float TimeLeft { get; private set; }
	public bool IsGameOver { get; private set; } = false;

	// 難易度（速度倍率）：初期値 1.0
	public float SpeedMultiplier { get; private set; } = 1.0f;
	private float _elapsedTime = 0.0f; // プレイ経過時間

	public override void _Ready()
	{
		// シングルトンの設定
		Instance = this;
		
		var backButton = GetNode<Button>("BackButton");
		backButton.Pressed += OnBackButtonPressed;


		// UIノードの取得（パスは実際の構成に合わせて調整してください）
		// CanvasLayer(HUD)の下にある場合
		_scoreLabel = GetNode<Label>("HUD/ScoreLabel");
		_timeLabel = GetNode<Label>("HUD/TimeLabel");
		_gameOverLabel = GetNode<Label>("HUD/GameOverLabel");
		_gameOverLabel.Visible = false;

		TimeLeft = InitialTime;
		UpdateUI();
	}

	public override void _Process(double delta)
	{
		if (IsGameOver) return;

		// 1. 時間の減少
		TimeLeft -= (float)delta;
		_elapsedTime += (float)delta;

		// 2. 難易度（スピード）の上昇
		// 例: プレイ時間10秒ごとに0.1ずつ速くなる
		SpeedMultiplier = 1.0f + (_elapsedTime / 10.0f) * 0.1f;

		// 3. ゲームオーバー判定
		if (TimeLeft <= 0)
		{
			TimeLeft = 0;
			GameOver();
		}

		UpdateUI();
	}

	// スコア加算と時間延長（Train.csから呼ばれる）
	public void AddScore(int amount, float timeBonus)
	{
		if (IsGameOver) return;

		Score += amount;
		TimeLeft += timeBonus; // 時間延長
		
		UpdateUI();
	}

	// ペナルティ（Dummyクリック時など）
	public void ApplyPenalty(float timePenalty)
	{
		if (IsGameOver) return;

		TimeLeft -= timePenalty;
		// 赤文字にするなどの演出を入れても良い
	}

	private void UpdateUI()
	{
		_scoreLabel.Text = $"Score: {Score}";
		// f1 は「小数点以下1桁まで」の意味
		_timeLabel.Text = $"Time: {TimeLeft.ToString("f1")}";
	}

	private void GameOver()
	{
		IsGameOver = true;
		_gameOverLabel.Visible = true;
		GD.Print($"Game Over! Final Score: {Score}");
		
		// ここでリザルト画面を出したり、ボタンを出してリトライさせたりする
		// 今回は簡易的に、3秒後にタイトルに戻す処理を入れる例
		GetTree().CreateTimer(3.0).Timeout += () => 
		{
			GetTree().ChangeSceneToFile("res://scenes/title/title_scene.tscn");
		};
	}
	
	public void OnBackButtonPressed()
	{
		// シーン遷移
		GetTree().ChangeSceneToFile("res://scenes/title/title_scene.tscn");
	}
}
