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
	private AudioStreamPlayer3D hitsound;

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
		globals = (Globals)GetNode("/root/Globals");
		
        // instantiate the players
		tankscene = ResourceLoader.Load("res://Tank.tscn") as Godot.PackedScene;
		
		// load audio
		hitsound = (AudioStreamPlayer3D) GetNode("hitsound");
		
		// signal for HUD to listen for
		AddUserSignal("tanks_created");
		
		Tank localplayer = (Tank) tankscene.Instance();
		localplayer.SetName(GetTree().GetNetworkUniqueId().ToString());
		localplayer.SetNetworkMaster(GetTree().GetNetworkUniqueId());
		
		Tank remoteplayer = (Tank) tankscene.Instance();
		remoteplayer.SetName(globals.otherPlayerId.ToString());
		remoteplayer.SetNetworkMaster(globals.otherPlayerId);
		
		Vector3 serverspawn = new Vector3(12, 0, 12);
		Vector3 clientspawn = new Vector3(-12, 0, -12);
		
		// set spawn positions
		if(GetTree().IsNetworkServer()) {
			localplayer.Translate(serverspawn);
			remoteplayer.Translate(clientspawn);
		} else {
			localplayer.Translate(clientspawn);
			remoteplayer.Translate(serverspawn);
		}
		localplayer.SetRespawn();
		remoteplayer.SetRespawn();
		
		AddChild(localplayer);
		AddChild(remoteplayer);
		
		// connect signals for tanks
		localplayer.Connect("bullet_fired", this, nameof(newBullet));
		remoteplayer.Connect("bullet_fired", this, nameof(newBullet));
		
		localplayer.SetActiveCam();
		
		// Notify the HUD that the tanks are available
		EmitSignal("tanks_created");
    }

	private void newBullet(Node bullet)
	{
		RigidBody b = (RigidBody) bullet;
		b.Connect("body_entered", this, nameof(playHitAudio));
	}
	
	private void playHitAudio(Node who)
	{
		hitsound.Play();
	}
//  // Called every frame. 'delta' is the elapsed time since the previous frame.
//  public override void _Process(float delta)
//  {
//      
//  }
}
