﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Models.WholeFarm.Resources
{
	/// <summary>
	/// Object for an individual female Ruminant.
	/// </summary>

	public class RuminantFemale : Ruminant
	{
		// Female Ruminant properties

		/// <summary>
		/// The age of female at last birth
		/// </summary>
		public double AgeAtLastBirth;

		/// <summary>
		/// Number of births for the female (twins = 1 birth)
		/// </summary>
		public int NumberOfBirths;

		/// <summary>
		/// Births this timestep
		/// </summary>
		public int NumberOfBirthsThisTimestep;
		
		/// <summary>
		/// The age at last conception
		/// </summary>
		public double AgeAtLastConception;

		/// <summary>
		/// Weight at time of conception
		/// </summary>
		public double WeightAtConception;

		/// <summary>
		/// Previous conception rate
		/// </summary>
		public double PreviousConceptionRate;

		/// <summary>
		/// Indicates if birth is due this month
		/// Knows whether the feotus(es) have survived
		/// </summary>
		public bool BirthDue
		{
			get
			{
				if(SuccessfulPregnancy)
				{
					return this.Age >= this.AgeAtLastConception + this.BreedParams.GestationLength & this.AgeAtLastConception > this.AgeAtLastBirth;
				}
				else
				{
					return false;
				}
			}
		}

		/// <summary>
		/// Method to handle birth changes
		/// </summary>
		public void UpdateBirthDetails()
		{
			if (SuccessfulPregnancy)
			{
				NumberOfBirths++;
				NumberOfBirthsThisTimestep = (CarryingTwins ? 2 : 1);
			}
			AgeAtLastBirth = this.Age;
//			SuccessfulPregnancy = false;
		}

		/// <summary>
		/// Indicates if the individual is pregnant
		/// </summary>
		public bool IsPregnant
		{
			get
			{
				return (this.Age < this.AgeAtLastConception + this.BreedParams.GestationLength & this.SuccessfulPregnancy);
			}
		}

		/// <summary>
		/// Indicates if individual is carrying twins
		/// </summary>
		public bool CarryingTwins;

		/// <summary>
		/// Method to remove one offspring that dies between conception and death
		/// </summary>
		public void OneOffspringDies()
		{
			if(CarryingTwins)
			{
				CarryingTwins = false;
			}
			else
			{
				SuccessfulPregnancy = false;
				AgeAtLastBirth = this.Age;

			}
		}

		/// <summary>
		/// Method to handle conception changes
		/// </summary>
		public void UpdateConceptionDetails(bool Twins, double Rate)
		{
			// if she was dry breeder remove flag as she has become pregnant.
			if (SaleFlag == HerdChangeReason.DryBreederSale)
			{
				SaleFlag = HerdChangeReason.None;
			}
			PreviousConceptionRate = Rate;
			CarryingTwins = Twins;
			WeightAtConception = this.Weight;
			AgeAtLastConception = this.Age;
			SuccessfulPregnancy = true;
		}

		/// <summary>
		/// Indicates if the individual is a dry breeder
		/// </summary>
		public bool DryBreeder;

		/// <summary>
		/// Indicates if the individual is lactating
		/// </summary>
		public bool IsLactating
		{
			get
			{
				return (this.AgeAtLastBirth > this.AgeAtLastConception && (this.Age - this.AgeAtLastBirth)*30.4 <= this.BreedParams.MilkingDays && SuccessfulPregnancy);
			}			
		}

		/// <summary>
		/// Calculate the MilkinIndicates if the individual is lactating
		/// </summary>
		public double DaysLactating
		{
			get
			{
				if(IsLactating)
				{
					return (((this.Age - this.AgeAtLastBirth)*30.4 <= this.BreedParams.MilkingDays)? (this.Age - this.AgeAtLastBirth) * 30.4 : 0);
				}
				else
				{
					return 0;
				}
			}
		}

		/// <summary>
		/// Amount of milk available in the month (L)
		/// </summary>
		public double MilkAmount;

		/// <summary>
		/// Amount of milk produced (L/day)
		/// </summary>
		public double MilkProduction;

		/// <summary>
		/// Method to remove milk from female
		/// </summary>
		/// <param name="amount">Amount to take</param>
		public void TakeMilk(double amount)
		{
			amount = Math.Min(amount, MilkAmount);
			MilkAmount -= amount;
		}

		/// <summary>
		/// A list of individuals currently suckling this female
		/// </summary>
		public List<Ruminant> SucklingOffspring;

		/// <summary>
		/// Used to track successful preganacy
		/// </summary>
		public bool SuccessfulPregnancy;

		/// <summary>
		/// Constructor
		/// </summary>
		public RuminantFemale()
		{
			SuccessfulPregnancy = false;
			SucklingOffspring = new List<Ruminant>();
		}
	}
}
