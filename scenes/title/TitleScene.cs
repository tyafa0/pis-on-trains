using Godot;
using System;

public partial class TitleScene : Control
{
	public override void _Ready()
	{
		// 1. ボタンのノードを取得する
		// ※ "Button" の部分は、シーンツリー上の実際のボタンの名前に変えてください
		var startButton = GetNode<Button>("Button");

		// 2. シグナル（イベント）にメソッドを登録する
		// C#の標準的なイベント購読構文 (+=) が使えます
		startButton.Pressed += OnStartButtonPressed;
	}
	
	public void OnStartButtonPressed()
	{
		// シーン遷移
		GetTree().ChangeSceneToFile("res://scenes/game/game_scene.tscn");
	}
}
