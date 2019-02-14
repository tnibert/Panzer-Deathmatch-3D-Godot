using Godot;
using System;

/*
Code links:
http://gameprogrammingpatterns.com/observer.html

Math links:
https://docs.godotengine.org/en/3.0/tutorials/math/vector_math.html#doc-vector-math
https://docs.godotengine.org/en/3.0/tutorials/math/vectors_advanced.html#doc-vectors-advanced
https://docs.godotengine.org/en/3.0/tutorials/math/matrices_and_transforms.html
https://docs.godotengine.org/en/3.0/tutorials/3d/using_transforms.html
https://www.khanacademy.org/math/linear-algebra
https://www.mathsisfun.com/algebra/vectors-dot-product.html

unit vectors == direction vectors == normals

dot product tells you if a vector is more, less, or equal to 90 degrees
	- can be used to determine if one entity is facing another
todo: research cross product more - perpendicular vector between two vectors

Unit normal vectors (often abbreviated normals):
"Unit vectors that are perpendicular to a surface (so, they describe the orientation of the surface)"

The dot product between a unit vector and any point in space returns the distance from the point to the plane.
If it is below the plane, value will be negative, can determine side of plane point is on.

Planes have polarity (switch polarity by * -1, operator '-' implemented in godot).  Godot has a Plane type.
N.Dot(point) - D; 
plane.DistanceTo(point);
^ the same, distance from plane to point.  D is distance from origin to plane, N is plane.

todo: learn more about quaternions

Use IsNetworkServer() to control collision detection

*/

public class Tank : KinematicBody
{
	// Keep references to our children, might be best to structure later
	private MeshInstance trackleft;
	private MeshInstance trackright;
	private MeshInstance body;
	private KinematicBody turret;
	private MeshInstance gun;
	private PackedScene bulletscene;
	private Camera thirdpersoncamera;
	private Camera firstpersoncamera;
	
	private Vector3 direction = new Vector3();
	
	// these speeds are calculated in with other variables, they are not equivalent
	private int speed = 200;			// will be multiplied by delta
	private int bulletspeed = 10;
	private float rotspeed = 0.7f;
	private float turretrotspeed = 0.2f;
	private float rotrad = 0;
	private float pitch = 0;
	private float yaw = 0;
	
	private Transform spawnpoint;
	private int maxhealth = 6;
	protected int health;
	private bool firstperson = false;
	
	private Vector2 currentmousepos = new Vector2();
	
	const Input.MouseMode MOUSE_MODE_CONFINED = (Input.MouseMode) 3;
	const Input.MouseMode MOUSE_MODE_CAPTURED = (Input.MouseMode) 2;
	const Input.MouseMode MOUSE_MODE_HIDDEN = (Input.MouseMode) 1;
	const Input.MouseMode MOUSE_MODE_VISIBLE = (Input.MouseMode) 0;

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
		// Get references to parts of tanks
        trackleft = (MeshInstance) GetNode("TrackLeft");
		trackright = (MeshInstance) GetNode("TrackRight");
		body = (MeshInstance) GetNode("Body");
		turret = (KinematicBody) GetNode("Turret");
		gun = (MeshInstance) GetNode("Turret/TurretMesh/Gun");
		
		thirdpersoncamera = (Camera) GetNode("ThirdPersonCam");
		firstpersoncamera = (Camera) GetNode("Turret/TurretMesh/Gun/BulletSpawn/Camera");
		
		// Load bullet scene
		bulletscene = ResourceLoader.Load("res://Bullet.tscn") as Godot.PackedScene;
		
		// Set a color for the tank
		Color tankcolor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
		foreach(MeshInstance mesh in new MeshInstance[] {trackleft, trackright, body, (MeshInstance) GetNode("Turret/TurretMesh"), gun})
		{
			SpatialMaterial mat = (SpatialMaterial) mesh.GetSurfaceMaterial(0);
			mat.AlbedoColor = tankcolor;
			mesh.SetSurfaceMaterial(0, mat);
		}
		health = maxhealth;
		
		Input.SetMouseMode(MOUSE_MODE_CONFINED);
		
		SetProcess(true);
		
		// required to appear at spawn
		MoveAndSlide(new Vector3(0, 0, 0), new Vector3(0, 1, 0), true);
    }

    // Called every frame. 'delta' is the elapsed time since the previous frame.
    /*public override void _Process(float delta)
    {
      
    }*/

	public override void _PhysicsProcess(float delta)
	{
		//GD.Print(Transform);
		//GD.Print("in physics process");
		direction = new Vector3(0, 0, 0);
		rotrad = 0;
		//float TurretRot = 0;
		bool change = false;
		
		if(IsNetworkMaster())
		{
			if(Input.IsActionPressed("ui_left"))
			{
				rotrad += rotspeed * delta;
				//Rotate(Vector3.Left, Mathf.Pi);
				change = true;
			}
			// repeat for other directions
			if(Input.IsActionPressed("ui_right"))
			{
				rotrad -= rotspeed * delta;
				//Rotate(Vector3.Right, Mathf.Pi);
				change = true;
			}
			if(Input.IsActionPressed("ui_up"))
			{
				//direction = new Vector3((float) Math.Cos(rotation.y), 0, (float) Math.Sin(rotation.y));
				//vel = new Vector3(0, 1, 0).Rotated(new Vector3(0,0,1), rotrad * Mathf.Pi) * speed * delta;
				direction = GetTransform().basis.z;
				change = true;
			}
			if(Input.IsActionPressed("ui_down"))
			{
				direction = -1 * GetTransform().basis.z;
				change = true;
			}

			// Only true on frame that mouse was pressed
			if(Input.IsActionJustPressed("left_mouse"))
			{
				Rpc("NetFire");
				Fire();
			}
			if(Input.IsActionJustPressed("right_mouse"))
			{
				SwapCamera();
			}
			
			// handle mouse movement
			// example: https://github.com/Veraball/veraball/blob/master/data/scripts/game/ball.gd#L43-L52
			/*
			# Mouse look
			# fmod() returns floating point remainder
			func _input(event):
				if event.type == InputEvent.MOUSE_MOTION and Input.get_mouse_mode() == Input.MOUSE_MODE_CAPTURED:
					yaw = fmod(yaw - event.relative_x * Game.view_sensitivity * 0.05, 360)
					# Prevent yaw from becoming negative:
					if yaw < 0:
						yaw = 359.75
					pitch = max(min(pitch - event.relative_y * Game.view_sensitivity * 0.05, 89), -89)
					yaw_node.set_rotation(Vector3(0, deg2rad(yaw), 0))
					pitch_node.set_rotation(Vector3(deg2rad(pitch), 0, 0))
			*/
			Vector2 newmousepos = GetViewport().GetMousePosition();
			if(newmousepos != currentmousepos)
			{
				// difference is equivalent to (relative_x, relative_y)
				Vector2 difference = currentmousepos - newmousepos;
				currentmousepos = newmousepos;
				
				//TurretRot = turretrotspeed * delta * difference.x;
				yaw = ((yaw - (difference.x * delta)) % 360);
				if(yaw < 0)
				{
					yaw = 359.75f;
				}
				
				//GD.Print(yaw);
				/*pitch = pitch - difference.y % 360;
				if(pitch < 0)
				{
					pitch = 359.75f;
				}*/
				
				change = true;
				
				// wrap mouse around if at edge of viewport
				//int offset = 10;
				if(currentmousepos.x >= GetViewport().GetSize().x-1)
				{
					Input.WarpMousePosition(new Vector2(1, currentmousepos.y));
					currentmousepos = GetViewport().GetMousePosition();
				}
				else if(currentmousepos.x <= 0)
				{
					Input.WarpMousePosition(new Vector2(GetViewport().GetSize().x-1, currentmousepos.y));
					currentmousepos = GetViewport().GetMousePosition();
				}
			}
			
			if(Input.IsActionJustPressed("ui_cancel"))
			{
				if(Input.GetMouseMode() == MOUSE_MODE_CONFINED)
				{
					Input.SetMouseMode(MOUSE_MODE_VISIBLE);
				}
				else
				{
					Input.SetMouseMode(MOUSE_MODE_CONFINED);
				}
			}
		}
		//GD.Print(delta);
		//GD.Print(direction);
		//GD.Print("------");		
		
		direction = direction.Normalized();
		direction = direction * speed * delta;
		//SetRotation(rotation);
		/*
		To move in a direction based on rotation -
		 - y (up/down) of the vector must be 0
		 - z is backward forward
		 - x is left right
		new Vector2((float)Math.Cos(radians), (float)Math.Sin(radians))
		^ inadequate
		https://docs.godotengine.org/en/3.0/tutorials/3d/using_transforms.html
		*/
		//LinearVelocity = Transform.basis.z * speed;
		
		/*
		Velocity measures the change in position per unit of time.
		The new position is found by adding velocity to the previous position.
		*/
		
		if(change)
		{
			LocSetPosAndRot(direction, rotrad, deg2rad(yaw), deg2rad(pitch));
			
			// announce movement
			RpcUnreliable("NetSetTransforms", this.Transform, turret.Transform);
		}
		
		// we don't need this, cause out of scope after, but I like being explicit for sanity, blame biomed
		change = false;
	}
	
	/*public override void _Input(InputEvent @event)
	{
		// This is kind of a hack to get our mouse to direct our turret rotation
	    // Relative mouse position
	    if (@event is InputEventMouseMotion eventMouseMotion)
	        GD.Print("Mouse Motion at: ", eventMouseMotion.GetRelative());
	
	    // Print the size of the viewport
	    //GD.Print("Viewport Resolution is: ", GetViewPortRect().Size);
	}*/
	
	private float deg2rad(float val)
	{
		// yes, it's a shitty hack, but I'm on an airplane right now with no internet
		double ratio = 57.324840764331206;
		return (float) (val/ratio);
	}
	
	private void LocSetPosAndRot(Vector3 dir, float bodyrot, float yaw, float pitch)
	{
		/*
		Do the transformations to the player
		*/
		RotateY(bodyrot);
		RotateTurret(yaw, pitch);
		MoveAndSlide(dir, new Vector3(0, 1, 0), true);
	}
	
	[Remote]
	private void NetSetTransforms(Transform bodytrans, Transform turrettrans)
	{
		/* syncs the transforms for the tank */
		//GD.Print(GetTree().GetNetworkUniqueId().ToString());
		
		this.Transform = bodytrans;
		turret.Transform = turrettrans;
	}
	
	private void RotateTurret(float yaw, float pitch)
	{
		/*
		To do turret rotation vertically, we will likely need to use quaternion here
		
		For some reason, when we rotate by two directions, the turret moves all over the place (around the tank)
		*/
		
		//GD.Print(yaw);
		turret.RotateY(yaw);
		//turret.RotateX(pitch);
		//turret.SetRotation(new Vector3(pitch, yaw, 0));
	}
	
	private void Fire()
	{
		//GD.Print("Fire");
		
		// instantiate bullet
		RigidBody bullet = (RigidBody) bulletscene.Instance();
		Spatial bulletspawnpoint = (Spatial) GetNode("Turret/TurretMesh/Gun/BulletSpawn");
		Vector3 spawnpos = bulletspawnpoint.GetGlobalTransform().origin;		//.GetTranslation() for local space
		
		// set position and velocity
		bullet.SetTranslation(spawnpos);
		bullet.SetLinearVelocity(turret.GetGlobalTransform().basis.z * bulletspeed);
		
		// add to scene
		GetParent().AddChild(bullet);
		bullet.Show();
	}
	
	[Remote]
	private void NetFire()
	{
		/*
		Physics engine does not produce same results across network
		Make bullet kinematicbody?
		*/
		Fire();
	}
	
	public int DecrementHealth()
	{
		health--;
		if(health <= 0)
		{
			//GD.Print("DEAD!");
			// explode and reset
			Respawn();
		}
		return health;
	}
	
	public void SetRespawn()
	{
		// Set the respawn point to the current position
		spawnpoint = GetTransform();
	}
	
	private void Respawn()
	{
		this.Transform = spawnpoint;
		health = maxhealth;
		// required to appear at spawn
		MoveAndSlide(new Vector3(0, 0, 0), new Vector3(0, 1, 0), true);
	}
	
	public Camera SetActiveCam()
	{
		thirdpersoncamera.MakeCurrent();
		firstperson = false;
		return thirdpersoncamera;
	}
	
	private void SwapCamera()
	{
		/*
		Switch between first person and third person cameras
		*/
		if(!firstperson)
		{
			firstpersoncamera.MakeCurrent();
		}
		else
		{
			thirdpersoncamera.MakeCurrent();
		}
		firstperson = !firstperson;
	}
}
