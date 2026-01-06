using Godot;
using System;

// 種類の定義
public enum TrainType
{
	CharA,
	CharB,
	Mixed, // AとBの両方
	Dummy
}

public partial class Train : Area2D
{
	[Export] public float Speed = 1600.0f;
	[Export] public AudioStream CorrectSound; // 正解音
	[Export] public AudioStream WrongSound;
	public Vector2 MoveDirection = Vector2.Right;
	
	// ★変更: _isCollected フラグをメインで使うように統合
	private bool _isCollected = false;

	public TrainType CurrentType { get; private set; } = TrainType.Dummy;

	private Sprite2D _sprite;
	private AudioStreamPlayer _sfxPlayer;

	public override void _Ready()
	{
		_sprite = GetNode<Sprite2D>("Sprite2D");
		_sfxPlayer = GetNode<AudioStreamPlayer>("SFXPlayer");

		var notifier = GetNode<VisibleOnScreenNotifier2D>("VisibleOnScreenNotifier2D");
		notifier.ScreenExited += OnScreenExited;
		InputEvent += OnInputEvent;
	}

	public void Initialize(TrainType type, Vector2 direction)
	{
		CurrentType = type;
		MoveDirection = direction;

		// 向きによる反転
		if (direction.X < 0)
		{
			_sprite.FlipH = true;
		}
		else
		{
			_sprite.FlipH = false;
		}

		// 色の設定（シェーダーがあってもModulateは基本色として機能します）
		switch (type)
		{
			case TrainType.CharA: _sprite.Modulate = Colors.Red; break;
			case TrainType.CharB: _sprite.Modulate = Colors.Blue; break;
			case TrainType.Mixed: _sprite.Modulate = Colors.Purple; break;
			case TrainType.Dummy: _sprite.Modulate = Colors.Black; break;
		}
	}

	public override void _Process(double delta)
	{
		if (GameScene.Instance.IsGameOver) return;

		// ★重要: 回収済み(_isCollected)であっても、動きは止めない！
		// これによりSpawnerが「まだ電車が走っている」と認識して距離計算を行える
		float currentSpeed = Speed * GameScene.Instance.SpeedMultiplier;
		Position += MoveDirection * currentSpeed * (float)delta;
	}

	private void OnScreenExited()
	{
		// 画面外に出た時だけ本当に削除する
		QueueFree();
	}

	private void OnInputEvent(Node viewport, InputEvent @event, long shapeIdx)
	{
		// 既に回収済み、またはゲームオーバーなら何もしない
		if (_isCollected || GameScene.Instance.IsGameOver) return;

		if (@event is InputEventMouseButton mouseEvent && mouseEvent.Pressed && mouseEvent.ButtonIndex == MouseButton.Left)
		{
			// ★処理実行
			ProcessClick();
		}
	}

	private void ProcessClick()
	{
		if (CurrentType == TrainType.Dummy)
		{
			// ダミーならペナルティ
			GameScene.Instance.ApplyPenalty(5.0f);
			GD.Print("お手つき！");
			
			PlaySound(WrongSound);
		}
		else
		{
			// 正解なら加点
			int points = 100;
			if (CurrentType == TrainType.Mixed) points = 300;

			GameScene.Instance.AddScore(points, 1.0f);
			PlaySound(CorrectSound);
		}

		// ★変更点: ここで QueueFree() せず、無効化処理を行う
		Deactivate();
	}
	
	private void PlaySound(AudioStream stream)
	{
		if (_sfxPlayer != null && stream != null)
		{
			_sfxPlayer.Stream = stream;
			_sfxPlayer.Play();
		}
	}

	// ★追加: 無効化処理（見た目を白黒にして、当たり判定を消す）
	private void Deactivate()
	{
		_isCollected = true;

		// 1. 当たり判定を物理的に消す（もうクリックできないように）
		// ※物理演算の途中での変更なので SetDeferred を使うのが安全
		GetNode<CollisionShape2D>("CollisionShape2D").SetDeferred("disabled", true);

		// 2. シェーダーを使ってグレースケール化
		if (_sprite.Material is ShaderMaterial mat)
		{
			mat.SetShaderParameter("is_grayscale", true);
		}
		else
		{
			// シェーダーが設定されていない場合の予備策：半透明にする
			// Modulate のアルファ値を下げる
			Color c = _sprite.Modulate;
			_sprite.Modulate = new Color(c.R, c.G, c.B, 0.3f);
		}

		// (オプション) 重なり順を後ろに下げて、他の生きてる電車の邪魔にならないようにする
		ZIndex = -1;
	}
}
