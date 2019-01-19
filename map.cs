using Godot;
using System;

public class map : Spatial
{
	/*
	This is poorly named, but this is the root object for our gameplay
	*/
	
    private PackedScene tankscene;
	private KinematicBody localplayer;
	private KinematicBody remoteplayer;
	private Globals globals;

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
		globals = (Globals)GetNode("/root/Globals");
		
        // instantiate the players
		tankscene = ResourceLoader.Load("res://Tank.tscn") as Godot.PackedScene;
		
		KinematicBody localplayer = (KinematicBody) tankscene.Instance();
		localplayer.SetName(GetTree().GetNetworkUniqueId().ToString());
		localplayer.SetNetworkMaster(GetTree().GetNetworkUniqueId());
		
		KinematicBody remoteplayer = (KinematicBody) tankscene.Instance();
		remoteplayer.SetName(globals.otherPlayerId.ToString());
		remoteplayer.SetNetworkMaster(globals.otherPlayerId);
		
		AddChild(localplayer);
		AddChild(remoteplayer);
		
    }

//  // Called every frame. 'delta' is the elapsed time since the previous frame.
//  public override void _Process(float delta)
//  {
//      
//  }
}
