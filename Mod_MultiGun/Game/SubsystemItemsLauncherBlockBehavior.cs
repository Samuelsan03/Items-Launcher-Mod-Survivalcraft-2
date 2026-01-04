using System;
using System.Collections.Generic;
using Engine;
using Game;
using TemplatesDatabase;

namespace Game
{
	public class SubsystemItemsLauncherBlockBehavior : SubsystemBlockBehavior
	{
		public override int[] HandledBlocks
		{
			get
			{
				return new int[]
				{
					ItemsLauncherBlock.Index
				};
			}
		}

		public override bool OnEditInventoryItem(IInventory inventory, int slotIndex, ComponentPlayer componentPlayer)
		{
			componentPlayer.ComponentGui.ModalPanelWidget = new AutoCannonWidget(componentPlayer, slotIndex);
			return true;
		}

		public override bool OnAim(Ray3 aim, ComponentMiner miner, AimState state)
		{
			if (miner.Inventory == null)
			{
				return false;
			}

			int slotValue = miner.Inventory.GetSlotValue(miner.Inventory.ActiveSlotIndex);
			if (Terrain.ExtractContents(slotValue) != ItemsLauncherBlock.Index)
			{
				return false;
			}

			int data = Terrain.ExtractData(slotValue);
			int num = ItemsLauncherBlock.GetRateLevel(data);
			if (num == 0)
			{
				num = 2;
			}

			switch (state)
			{
				case AimState.InProgress:
					{
						ComponentFirstPersonModel componentFirstPersonModel = miner.Entity.FindComponent<ComponentFirstPersonModel>();
						if (componentFirstPersonModel != null)
						{
							ComponentPlayer componentPlayer = miner.ComponentPlayer;
							if (componentPlayer != null)
							{
								componentPlayer.ComponentAimingSights.ShowAimingSights(aim.Position, aim.Direction);
							}
							componentFirstPersonModel.ItemOffsetOrder = new Vector3(-0.21f, 0.15f, 0.08f);
							componentFirstPersonModel.ItemRotationOrder = new Vector3(-0.7f, 0f, 0f);
						}

						miner.ComponentCreature.ComponentCreatureModel.AimHandAngleOrder = 1.4f;
						miner.ComponentCreature.ComponentCreatureModel.InHandItemOffsetOrder = new Vector3(-0.08f, -0.08f, 0.07f);
						miner.ComponentCreature.ComponentCreatureModel.InHandItemRotationOrder = new Vector3(-1.7f, 0f, 0f);

						if (num > 1)
						{
							float num2 = SubsystemItemsLauncherBlockBehavior.m_rateValues[num - 1];
							double gameTime = this.m_subsystemTime.GameTime;
							double num3;
							if (!this.m_nextFireTimes.TryGetValue(miner, out num3))
							{
								num3 = gameTime + 0.2;
								this.m_nextFireTimes[miner] = num3;
							}

							if (gameTime >= num3)
							{
								this.Fire(miner, aim);
								this.m_nextFireTimes[miner] = gameTime + 1.0 / (double)num2;
							}
						}
						break;
					}
				case AimState.Cancelled:
					this.m_nextFireTimes.Remove(miner);
					break;
				case AimState.Completed:
					if (num == 1)
					{
						this.Fire(miner, aim);
					}
					this.m_nextFireTimes.Remove(miner);
					break;
			}

			return false;
		}

		private void Fire(ComponentMiner miner, Ray3 aim)
		{
			IInventory inventory = miner.Inventory;
			int activeSlotIndex = miner.Inventory.ActiveSlotIndex;
			int slotValue = inventory.GetSlotValue(activeSlotIndex);
			int data = Terrain.ExtractData(slotValue);
			GameMode gameMode = this.m_subsystemGameInfo.WorldSettings.GameMode;

			// Verificar si hay fuel disponible
			bool hasFuel = false;
			if (gameMode > 0)
			{
				int fuel = ItemsLauncherBlock.GetFuel(data);
				hasFuel = fuel > 0;

				// Consumir fuel si está disponible
				if (hasFuel)
				{
					int num3 = ItemsLauncherBlock.SetFuel(data, fuel - 1);
					int num4 = Terrain.ReplaceData(slotValue, num3);
					inventory.RemoveSlotItems(activeSlotIndex, 1);
					inventory.AddSlotItems(activeSlotIndex, num4, 1);
				}
			}

			int num = 0;
			int num2 = -1;
			for (int i = 0; i < 10; i++)
			{
				if (i != activeSlotIndex && inventory.GetSlotCount(i) > 0)
				{
					num = inventory.GetSlotValue(i);
					num2 = i;
					break;
				}
			}

			if (num2 != -1)
			{
				// Configurar parámetros de disparo
				int num5 = ItemsLauncherBlock.GetSpeedLevel(data);
				int num6 = ItemsLauncherBlock.GetSpreadLevel(data);
				if (num5 == 0) num5 = 2;
				if (num6 == 0) num6 = 2;

				float num7 = SubsystemItemsLauncherBlockBehavior.m_speedValues[num5 - 1];
				float num8 = SubsystemItemsLauncherBlockBehavior.m_spreadValues[num6 - 1];
				Vector3 eyePosition = miner.ComponentCreature.ComponentCreatureModel.EyePosition;
				Vector3 vector = Vector3.Normalize(aim.Direction + num8 * new Vector3(this.m_random.Float(-1f, 1f), this.m_random.Float(-1f, 1f), this.m_random.Float(-1f, 1f)));

				SubsystemProjectiles subsystemProjectiles = base.Project.FindSubsystem<SubsystemProjectiles>(true);
				SubsystemAudio subsystemAudio = base.Project.FindSubsystem<SubsystemAudio>(true);
				SubsystemParticles subsystemParticles = base.Project.FindSubsystem<SubsystemParticles>(true);
				SubsystemTerrain subsystemTerrain = base.Project.FindSubsystem<SubsystemTerrain>(true);

				// Disparar el proyectil
				subsystemProjectiles.FireProjectile(num, eyePosition, vector * num7, Vector3.Zero, miner.ComponentCreature);
				subsystemAudio.PlaySound("Audio/ItemsLauncher/Item Cannon Fire", 0.5f, this.m_random.Float(-0.1f, 0.1f), eyePosition, 10f, true);
				subsystemParticles.AddParticleSystem(new GunSmokeParticleSystem(subsystemTerrain, eyePosition + 0.5f * vector, vector), false);

				// Aplicar efectos solo si hay fuel
				if (gameMode > 0 && hasFuel)
				{
					miner.ComponentCreature.ComponentBody.ApplyImpulse(-4f * vector);
					this.m_subsystemNoise.MakeNoise(eyePosition, 1f, 15f);
				}

				// Remover el proyectil del inventario
				inventory.RemoveSlotItems(num2, 1);
			}
			else
			{
				// Mostrar mensaje cuando no hay munición
				ComponentPlayer componentPlayer = miner.ComponentPlayer;
				if (componentPlayer != null)
				{
					string message = LanguageControl.Get("SubsystemItemsLauncherBlockBehavior", "YouNeedAmmunition");
					componentPlayer.ComponentGui.DisplaySmallMessage(message, Color.Orange, true, false);
				}

				// MANTENER el sonido del martillo del lanzador de ítems
				base.Project.FindSubsystem<SubsystemAudio>(true).PlaySound("Audio/ItemsLauncher/Item Launcher Hammer Release", 1f, this.m_random.Float(-0.1f, 0.1f), miner.ComponentCreature.ComponentCreatureModel.EyePosition, 2f, false);
			}
		}

		public override void Load(ValuesDictionary valuesDictionary)
		{
			base.Load(valuesDictionary);
			this.m_subsystemTime = base.Project.FindSubsystem<SubsystemTime>(true);
			this.m_subsystemGameInfo = base.Project.FindSubsystem<SubsystemGameInfo>(true);
			this.m_subsystemNoise = base.Project.FindSubsystem<SubsystemNoise>(true);
		}

		public SubsystemTime m_subsystemTime;
		public SubsystemGameInfo m_subsystemGameInfo;
		public Game.Random m_random = new Game.Random();
		public SubsystemNoise m_subsystemNoise;

		private static readonly float[] m_speedValues = new float[]
		{
			10f, 35f, 60f
		};

		private static readonly float[] m_rateValues = new float[]
		{
			1f, 2f, 3f, 4f, 5f, 6f, 7f, 8f, 9f, 10f, 11f, 12f, 13f, 14f, 15f
		};

		private static readonly float[] m_spreadValues = new float[]
		{
			0.01f, 0.1f, 0.5f
		};

		private Dictionary<ComponentMiner, double> m_nextFireTimes = new Dictionary<ComponentMiner, double>();
	}
}