﻿using Models.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Models.CLEM.Resources;
using System.Xml.Serialization;
using System.ComponentModel.DataAnnotations;

namespace Models.CLEM.Activities
{
    /// <summary>Ruminant grazing activity</summary>
    /// <summary>Specific version where pasture and breed is specified</summary>
    /// <summary>This activity determines how a ruminant breed will graze on a particular pasture (GrazeFoodSotreType)</summary>
    [Serializable]
    [ViewName("UserInterface.Views.GridView")]
    [PresenterName("UserInterface.Presenters.PropertyPresenter")]
    [ValidParent(ParentType = typeof(CLEMActivityBase))]
    [ValidParent(ParentType = typeof(ActivitiesHolder))]
    [ValidParent(ParentType = typeof(ActivityFolder))]
    [Description("This activity performs grazing of a specified herd and pasture (paddock) in the simulation.")]
    class RuminantActivityGrazePastureHerd : CLEMRuminantActivityBase
    {
        /// <summary>
        /// Link to clock
        /// Public so children can be dynamically created after links defined
        /// </summary>
        [Link]
        public Clock Clock = null;

        /// <summary>
        /// Number of hours grazed
        /// Based on 8 hour grazing days
        /// Could be modified to account for rain/heat walking to water etc.
        /// </summary>
        [Description("Number of hours grazed (based on 8 hr grazing day)")]
        [Required, Range(0, 8, ErrorMessage = "Value based on maximum 8 hour grazing day")]
        public double HoursGrazed { get; set; }

        /// <summary>
        /// Name of paddock or pasture to graze
        /// </summary>
        [Description("Name of GrazeFoodStoreType to graze")]
        [Required(AllowEmptyStrings = false, ErrorMessage = "Name of Graze Food Store required")]
        public string GrazeFoodStoreTypeName { get; set; }

        /// <summary>
        /// paddock or pasture to graze
        /// </summary>
        [XmlIgnore]
        public GrazeFoodStoreType GrazeFoodStoreModel { get; set; }

        /// <summary>
        /// Name of ruminant group to graze
        /// </summary>
        [Description("Name of ruminant type to graze")]
        [Required(AllowEmptyStrings = false, ErrorMessage = "Name of Ruminant Type required")]
        public string RuminantTypeName { get; set; }

        /// <summary>
        /// Ruminant group to graze
        /// </summary>
        [XmlIgnore]
        public RuminantType RuminantTypeModel { get; set; }

        /// <summary>
        /// The proportion of required graze that is available determined from parent activity arbitration
        /// </summary>
        public double GrazingCompetitionLimiter { get; set; }

        /// <summary>
        /// The biomass of pasture per hectare at start of allocation
        /// </summary>
        public double BiomassPerHectare { get; set; }

        /// <summary>
        /// Potential intake limiter based on pasture quality
        /// </summary>
        public double PotentialIntakePastureQualityLimiter { get; set; }

        /// <summary>
        /// Dry matter digestibility of pasture consumed (%)
        /// </summary>
        public double DMD { get; set; }

        /// <summary>
        /// Nitrogen of pasture consumed (%)
        /// </summary>
        public double N { get; set; }

        /// <summary>
        /// Proportion of intake that can be taken from each pool
        /// </summary>
        public List<GrazeBreedPoolLimit> PoolFeedLimits { get; set; }

        /// <summary>An event handler to allow us to initialise ourselves.</summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        [EventSubscribe("CLEMInitialiseActivity")]
        private void OnCLEMInitialiseActivity(object sender, EventArgs e)
        {
            // This method will only fire if the user has added this activity to the UI
            // Otherwise all details will be provided from GrazeAll or GrazePaddock code [CLEMInitialiseActivity]

            this.InitialiseHerd(true, true);

            // if no settings have been provided from parent set limiter to 1.0. i.e. no limitation
            if (GrazingCompetitionLimiter == 0) GrazingCompetitionLimiter = 1.0;

            GrazeFoodStoreModel = Resources.GetResourceItem(this, typeof(GrazeFoodStore), GrazeFoodStoreTypeName, OnMissingResourceActionTypes.ReportErrorAndStop, OnMissingResourceActionTypes.ReportErrorAndStop) as GrazeFoodStoreType;

            RuminantTypeModel = Resources.GetResourceItem(this, typeof(RuminantHerd), RuminantTypeName, OnMissingResourceActionTypes.ReportErrorAndStop, OnMissingResourceActionTypes.ReportErrorAndStop) as RuminantType;
        }

        /// <summary>An event handler to allow us to clear requests at start of month.</summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        [EventSubscribe("StartOfMonth")]
        private void OnStartOfMonth(object sender, EventArgs e)
        {
            ResourceRequestList = null;
            this.PoolFeedLimits = null;
        }

        /// <summary>
        /// Method to get the resources required for this activity
        /// this method overrides the base method to allow specific resource rules
        /// and not remove resources immediately
        /// </summary>
        public override void GetResourcesRequiredForActivity()
        {
            // if there is no ResourceRequestList (i.e. not created from parent pasture)
            if (ResourceRequestList == null)
            {
                // determine pasture quality from all pools (DMD) at start of grazing
                double pastureDMD = GrazeFoodStoreModel.DMD;

                // Reduce potential intake based on pasture quality for the proportion consumed (zero legume).
                // TODO: check that this doesn't need to be performed for each breed based on how pasture taken
                // NABSA uses Diet_DMD, but we cant adjust Potential using diet before anything consumed.
                PotentialIntakePastureQualityLimiter = 1.0;
                if ((0.8 - GrazeFoodStoreModel.IntakeTropicalQualityCoefficient - pastureDMD / 100) >= 0)
                {
                    PotentialIntakePastureQualityLimiter = 1 - GrazeFoodStoreModel.IntakeQualityCoefficient * (0.8 - GrazeFoodStoreModel.IntakeTropicalQualityCoefficient - pastureDMD / 100);
                }

                GetResourcesNeededForActivity();
            }
            // this has all the code to feed animals.
            DoActivity();
        }

        public override List<ResourceRequest> GetResourcesNeededForActivity()
        {
            // check if resource request list has been calculated from a parent call
            if (ResourceRequestList == null)
            {
                ResourceRequestList = new List<ResourceRequest>();
                List<Ruminant> herd = this.CurrentHerd(false).Where(a => a.Location == this.GrazeFoodStoreModel.Name & a.HerdName == this.RuminantTypeModel.Name).ToList();
                if (herd.Count() > 0)
                {
                    double amount = 0;
                    double indAmount = 0;
                    // get list of all Ruminants of specified breed in this paddock
                    foreach (Ruminant ind in herd)
                    {
                        // Reduce potential intake (monthly) based on pasture quality for the proportion consumed calculated in GrazePasture.
                        // calculate intake from potential modified by pasture availability and hours grazed
                        indAmount = ind.PotentialIntake * PotentialIntakePastureQualityLimiter * (1 - Math.Exp(-ind.BreedParams.IntakeCoefficientBiomass * this.GrazeFoodStoreModel.TonnesPerHectareStartOfTimeStep * 1000)) * (HoursGrazed / 8);
                        amount += indAmount;
                    }
                    // report even if zero so shortfalls can be reported.
                    //if (amount > 0)
                    //{
                        ResourceRequestList.Add(new ResourceRequest()
                        {
                            AllowTransmutation = true,
                            Required = amount,
                            ResourceType = typeof(GrazeFoodStore),
                            ResourceTypeName = this.GrazeFoodStoreModel.Name,
                            ActivityModel = this,
                            AdditionalDetails = this
                        }
                        );
                    //}

                    if ( GrazeFoodStoreTypeName != null & GrazeFoodStoreModel != null)
                    {
                        // Stand alone model has not been set by parent RuminantActivityGrazePasture
                        SetupPoolsAndLimits(1.0);
                    }
                }
            }
            return ResourceRequestList;
        }

        /// <summary>
        /// Method to set up pools from currently available graze pools and limit based upon green content herd limit parameters
        /// </summary>
        /// <param name="limit">The competition limit defined from GrazePasture parent</param>
        public void SetupPoolsAndLimits(double limit)
        {
            this.GrazingCompetitionLimiter = limit;
            // store kg/ha available for consumption calculation
            this.BiomassPerHectare = GrazeFoodStoreModel.kgPerHa;

            // calculate breed feed limits
            if (this.PoolFeedLimits == null)
            {
                this.PoolFeedLimits = new List<GrazeBreedPoolLimit>();
            }
            else
            {
                this.PoolFeedLimits.Clear();
            }

            foreach (var pool in GrazeFoodStoreModel.Pools)
            {
                this.PoolFeedLimits.Add(new GrazeBreedPoolLimit() { Limit = 1.0, Pool = pool });
            }

            // if Jan-March then user first three months otherwise use 2
            int greenage = (Clock.Today.Month <= 3) ? 3 : 2;

            double green = GrazeFoodStoreModel.Pools.Where(a => (a.Age <= greenage)).Sum(b => b.Amount);
            double propgreen = green / GrazeFoodStoreModel.Amount;
            
            // All valuesa re now proportions.
            // Convert to percentage before calculation
            
            double greenlimit = (this.RuminantTypeModel.GreenDietMax*100) * (1 - Math.Exp(-this.RuminantTypeModel.GreenDietCoefficient * ((propgreen*100) - (this.RuminantTypeModel.GreenDietZero*100))));
//            double greenlimit = this.RuminantTypeModel.GreenDietMax * (1 - Math.Exp(-this.RuminantTypeModel.GreenDietCoefficient * ((propgreen * 100.0) - this.RuminantTypeModel.GreenDietZero)));
            greenlimit = Math.Max(0.0, greenlimit);
            if (propgreen > 90)
            {
                greenlimit = 100;
            }

            foreach (var pool in this.PoolFeedLimits.Where(a => a.Pool.Age <= greenage))
            {
                pool.Limit = greenlimit / 100.0;
            }
        }

        public override void DoActivity()
        {
            //Go through amount received and put it into the animals intake with quality measures.
            if (ResourceRequestList != null)
            {
                List<Ruminant> herd = this.CurrentHerd(false).Where(a => a.Location == this.GrazeFoodStoreModel.Name & a.HerdName == this.RuminantTypeModel.Name).ToList();
                if (herd.Count() > 0)
                {
                    //Get total amount
                    double totalDesired = herd.Sum(a => a.PotentialIntake * PotentialIntakePastureQualityLimiter * (HoursGrazed / 8));
                    double totalEaten = herd.Sum(a => a.PotentialIntake * PotentialIntakePastureQualityLimiter * (1 - Math.Exp(-a.BreedParams.IntakeCoefficientBiomass * this.GrazeFoodStoreModel.TonnesPerHectareStartOfTimeStep * 1000)) * (HoursGrazed / 8));
                    totalEaten *= GrazingCompetitionLimiter;

                    // take resource
                    ResourceRequest request = new ResourceRequest()
                    {
                        ActivityModel = this,
                        AdditionalDetails = this,
                        Reason = RuminantTypeModel.Name+" grazing",
                        Required = totalEaten,
                        Resource = GrazeFoodStoreModel
                    };
                    GrazeFoodStoreModel.Remove(request);

                    FoodResourcePacket food = new FoodResourcePacket()
                    {
                        DMD = ((RuminantActivityGrazePastureHerd)request.AdditionalDetails).DMD,
                        PercentN = ((RuminantActivityGrazePastureHerd)request.AdditionalDetails).N
                    };

                    double shortfall = request.Provided / request.Required;

                    // allocate to individuals
                    foreach (Ruminant ind in herd)
                    {
                        double eaten = ind.PotentialIntake * PotentialIntakePastureQualityLimiter * (HoursGrazed / 8);
                        food.Amount = eaten * GrazingCompetitionLimiter * shortfall;
                        ind.AddIntake(food);
                    }
                    SetStatusSuccess();

                    // if insufficent provided or no pasture (nothing eaten) use totalNeededifPasturePresent
                    if (GrazingCompetitionLimiter < 1)
                    {
                        request.Available = request.Provided; // display all that was given
                        request.Required = totalDesired;
                        request.ResourceType = typeof(GrazeFoodStore);
                        request.ResourceTypeName = GrazeFoodStoreModel.Name;
                        ResourceRequestEventArgs rre = new ResourceRequestEventArgs() { Request = request };
                        OnShortfallOccurred(rre);

                        if(this.OnPartialResourcesAvailableAction == OnPartialResourcesAvailableActionTypes.ReportErrorAndStop)
                        {
                            throw new ApsimXException(this, "Insufficient pasture available for grazing in paddock ("+GrazeFoodStoreModel.Name+") in "+Clock.Today.Month.ToString()+"\\"+Clock.Today.Year.ToString());
                        }
                        this.Status = ActivityStatus.Partial;
                    }
                }
            }
        }

        /// <summary>
        /// Method to determine resources required for initialisation of this activity
        /// </summary>
        /// <returns></returns>
        public override List<ResourceRequest> GetResourcesNeededForinitialisation()
        {
            return null;
        }

        /// <summary>
        /// Resource shortfall event handler
        /// </summary>
        public override event EventHandler ResourceShortfallOccurred;

        /// <summary>
        /// Shortfall occurred 
        /// </summary>
        /// <param name="e"></param>
        protected override void OnShortfallOccurred(EventArgs e)
        {
            if (ResourceShortfallOccurred != null)
                ResourceShortfallOccurred(this, e);
        }

        /// <summary>
        /// Resource shortfall occured event handler
        /// </summary>
        public override event EventHandler ActivityPerformed;

        /// <summary>
        /// Shortfall occurred 
        /// </summary>
        /// <param name="e"></param>
        protected override void OnActivityPerformed(EventArgs e)
        {
            if (ActivityPerformed != null)
                ActivityPerformed(this, e);
        }

    }

}
