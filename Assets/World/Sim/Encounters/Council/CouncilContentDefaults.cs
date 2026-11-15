namespace World.Sim.Encounters.Council
{
    /// <summary>GENERATED copy of StreamingAssets/world/council/council_content.json (a test pins that they match). Edit the JSON, then regenerate.</summary>
    public static class CouncilContentDefaults
    {
        public const string Json = @"{
  ""Version"": 1,
  ""Offices"": { ""prehistoric"": ""chief"", ""historic"": ""reeve"", ""modern"": ""mayor"", ""future"": ""mayor"" },
  ""Emblems"": [
    { ""Id"": ""acorn"", ""Name"": ""Acorn"", ""Shape"": ""circle"", ""Colour"": ""green"" },
    { ""Id"": ""arrowhead"", ""Name"": ""Arrowhead"", ""Shape"": ""triangle"", ""Colour"": ""red"" },
    { ""Id"": ""hearthstone"", ""Name"": ""Hearthstone"", ""Shape"": ""square"", ""Colour"": ""blue"" },
    { ""Id"": ""leaf"", ""Name"": ""Willow leaf"", ""Shape"": ""diamond"", ""Colour"": ""gold"" }
  ],
  ""Personalities"": [
    { ""Id"": ""careful"", ""Name"": ""The Careful"", ""Description"": ""Banks early and takes few risks."", ""BustTolerance"": 8, ""MinAdvances"": 1, ""MaxAdvances"": 3, ""BankAtTopPercent"": 100, ""TrailingBoost"": 6, ""LeadingCaution"": 3, ""PetitionBoost"": 0, ""Impulse"": 5, ""WeightStep"": 1.0, ""WeightProgress"": 1.0, ""WeightTop"": 3.0, ""WeightNewCatspaw"": -1.5, ""WeightCentral"": 2.0, ""WeightAdvanced"": 1.0, ""Noise"": 0.5 },
    { ""Id"": ""steady"", ""Name"": ""The Steady"", ""Description"": ""Balances progress against risk."", ""BustTolerance"": 18, ""MinAdvances"": 1, ""MaxAdvances"": 5, ""BankAtTopPercent"": 90, ""TrailingBoost"": 8, ""LeadingCaution"": 4, ""PetitionBoost"": 0, ""Impulse"": 8, ""WeightStep"": 1.0, ""WeightProgress"": 1.5, ""WeightTop"": 3.0, ""WeightNewCatspaw"": 0.0, ""WeightCentral"": 1.0, ""WeightAdvanced"": 1.5, ""Noise"": 0.8 },
    { ""Id"": ""bold"", ""Name"": ""The Bold"", ""Description"": ""Presses on and spreads catspaws widely."", ""BustTolerance"": 34, ""MinAdvances"": 2, ""MaxAdvances"": 8, ""BankAtTopPercent"": 60, ""TrailingBoost"": 10, ""LeadingCaution"": 0, ""PetitionBoost"": 0, ""Impulse"": 10, ""WeightStep"": 1.0, ""WeightProgress"": 0.5, ""WeightTop"": 2.0, ""WeightNewCatspaw"": 1.0, ""WeightCentral"": -0.5, ""WeightAdvanced"": 0.0, ""Noise"": 1.0 },
    { ""Id"": ""closer"", ""Name"": ""The Closer"", ""Description"": ""Chases short tracks and banks the moment one is won."", ""BustTolerance"": 22, ""MinAdvances"": 1, ""MaxAdvances"": 6, ""BankAtTopPercent"": 100, ""TrailingBoost"": 8, ""LeadingCaution"": 2, ""PetitionBoost"": 0, ""Impulse"": 6, ""WeightStep"": 0.5, ""WeightProgress"": 3.0, ""WeightTop"": 6.0, ""WeightNewCatspaw"": -0.5, ""WeightCentral"": -2.0, ""WeightAdvanced"": 3.0, ""Noise"": 0.6 }
  ],
  ""Listening"": { ""Responses"": 5, ""AccuracyThresholdPercent"": 85, ""SpeedBonusPercent"": 5, ""FastMs"": 1500, ""SlowMs"": 4000, ""MaxBursts"": 3 },
  ""Fallbacks"": {
    ""DelegateTitle"": ""local delegate"",
    ""Note"": ""Existing residents fill a dispute's lead part when an occupation matches; otherwise a local delegate stands in. Dispute facts never need an occupation the settlement lacks."",
    ""DelegateNames"": [""Tove"", ""Brannoch"", ""Idris"", ""Maelle"", ""Hobb"", ""Sunniva"", ""Carrick"", ""Lysa"", ""Doran"", ""Peony"", ""Ruan"", ""Ysolt"", ""Gwen"", ""Torvald"", ""Marit"", ""Anselm""]
  },
  ""Names"": [""Aldis"", ""Bera"", ""Corin"", ""Dessa"", ""Edrin"", ""Fenna"", ""Garrow"", ""Hilde"", ""Iven"", ""Jorra"", ""Kelda"", ""Lorn"", ""Mavi"", ""Nell"", ""Perrin"", ""Quill"", ""Rhosa"", ""Sten"", ""Tamsin"", ""Ulva"", ""Varo"", ""Wynn"", ""Yara"", ""Zeb"", ""Alder"", ""Brisa"", ""Cael"", ""Dunn"", ""Emmet"", ""Fyra"", ""Gorm"", ""Hale"", ""Ilsa"", ""Joss"", ""Kiri"", ""Lars"", ""Mora"", ""Nils"", ""Odda"", ""Pell"", ""Rook"", ""Sorrel"", ""Tarn"", ""Una"", ""Vesna"", ""Wick"", ""Yew"", ""Zora""],
  ""Disputes"": [
    {
      ""Id"": ""crossing"", ""Track"": 2, ""Pattern"": ""absent worker"", ""Role"": ""ferryman"", ""Occupations"": [""ferryman""], ""FallbackTitle"": ""crossing delegate"",
      ""Props"": { ""prehistoric"": ""cracked paddle"", ""historic"": ""broken oar"" }, ""Silhouette"": ""long shaft with a flat blade"",
      ""Lines"": {
        ""opening"": [""My {prop} snapped and the crossing is shut. {candidate}, can you help me?"", ""Nobody crosses while the {prop} is broken. Will you speak for us, {candidate}?""],
        ""progress"": [""Better. Two people waded over today instead of none."", ""The bank is busier. Keep it up.""],
        ""setback"": [""I am sorry. I am tying up for the day."", ""That sank it. Come back when you have more to offer.""],
        ""revisit"": [""Still no crossing, {candidate}, but I am glad you came back."", ""The water is waiting, and so am I.""],
        ""agreement"": [""The {prop} will be mended. You have my word, {candidate}."", ""Fair enough. The crossing opens, and so does my support.""],
        ""acknowledgement"": [""{winner} got the crossing open. I still hear the splash."", ""The crossing runs because {winner} listened to me.""]
      }
    },
    {
      ""Id"": ""boundary"", ""Track"": 3, ""Pattern"": ""disputed ownership"", ""Role"": ""neighbour"", ""Occupations"": [""*""], ""FallbackTitle"": ""neighbour delegate"",
      ""Props"": { ""prehistoric"": ""boundary rope"", ""historic"": ""boundary rope"" }, ""Silhouette"": ""coiled rope with two stakes"",
      ""Lines"": {
        ""opening"": [""That {prop} was moved in the night. Whose strip is this, {candidate}?"", ""We cannot agree where my field ends. Help us, {candidate}.""],
        ""progress"": [""We are talking now, which is new."", ""The line is closer to the old stakes. Good.""],
        ""setback"": [""Enough. I am pulling the stakes out again."", ""No deal. We are not speaking.""],
        ""revisit"": [""Still crooked, {candidate}. Try once more."", ""The {prop} lies in the grass until you settle it.""],
        ""agreement"": [""Straight and fair. {candidate}, I accept the new line."", ""We shake on it. You may count on us, {candidate}.""],
        ""acknowledgement"": [""The boundary holds because {winner} walked it with us."", ""No more quarrels over the {prop}. Thanks to {winner}.""]
      }
    },
    {
      ""Id"": ""forest"", ""Track"": 4, ""Pattern"": ""forest dispute"", ""Role"": ""forester"", ""Occupations"": [""woodcutter"", ""forester""], ""FallbackTitle"": ""forest delegate"",
      ""Props"": { ""prehistoric"": ""sapling"", ""historic"": ""sapling"" }, ""Silhouette"": ""thin stem with a leafy tuft"",
      ""Lines"": {
        ""opening"": [""They fell trees faster than the forest grows. Plant this {prop} with me, {candidate}."", ""The ridge is bare. Will you help us replant, {candidate}?""],
        ""progress"": [""A few more saplings are in the ground."", ""Good. The cutters are slowing down.""],
        ""setback"": [""The cutters went back in. I cannot stop them alone."", ""Too late for today. The axes are loud again.""],
        ""revisit"": [""The stumps are still there, {candidate}."", ""Come back and help me guard the young trees.""],
        ""agreement"": [""We cut one, we plant two. {candidate}, you have my vote."", ""The ridge will be green again. Thank you, {candidate}.""],
        ""acknowledgement"": [""The young trees on the ridge are {winner}'s doing."", ""Because of {winner}, we plant before we fell.""]
      }
    },
    {
      ""Id"": ""timber"", ""Track"": 5, ""Pattern"": ""withheld resource"", ""Role"": ""carpenter"", ""Occupations"": [""carpenter"", ""sawyer""], ""FallbackTitle"": ""builders' delegate"",
      ""Props"": { ""prehistoric"": ""split log"", ""historic"": ""split beam"" }, ""Silhouette"": ""long beam with a crack down the middle"",
      ""Lines"": {
        ""opening"": [""This {prop} will not bear a roof. Someone is holding back the good timber."", ""I cannot build with a split {prop}, {candidate}. Find me sound wood.""],
        ""progress"": [""A better plank arrived. It is a start."", ""That is straighter. Keep it coming.""],
        ""setback"": [""The timber was taken away again. I am stuck."", ""Nothing today. I am putting my tools down.""],
        ""revisit"": [""Still waiting on sound wood, {candidate}."", ""The roof is open to the sky, remember.""],
        ""agreement"": [""Good wood at last. {candidate} has earned my hands."", ""The roof will stand. I am with you, {candidate}.""],
        ""acknowledgement"": [""Every sound beam I raise, I owe a little to {winner}."", ""{winner} found the timber, and the roof stands.""]
      }
    },
    {
      ""Id"": ""herbs"", ""Track"": 6, ""Pattern"": ""withheld resource"", ""Role"": ""healer"", ""Occupations"": [""healer""], ""FallbackTitle"": ""healing delegate"",
      ""Props"": { ""prehistoric"": ""empty clay pot"", ""historic"": ""empty jar"" }, ""Silhouette"": ""round-bellied pot with a narrow neck"",
      ""Lines"": {
        ""opening"": [""My {prop} is dry and the fever is spreading. {candidate}, who holds the herbs?"", ""I have nothing left to give the sick. Please help, {candidate}.""],
        ""progress"": [""A few herbs reached me. Thank you."", ""The pot is no longer empty. Do not stop.""],
        ""setback"": [""Someone shut the door on me. Nothing today."", ""No more herbs. The sick must wait.""],
        ""revisit"": [""Still thin on medicine, {candidate}."", ""The fever has not eased. Please try again.""],
        ""agreement"": [""The {prop} will be filled. I stand with {candidate}."", ""At last, herbs for the sick. You have my voice, {candidate}.""],
        ""acknowledgement"": [""The sick are mending, and {winner} is the reason."", ""{winner} filled my {prop}. I remember that.""]
      }
    },
    {
      ""Id"": ""grain"", ""Track"": 7, ""Pattern"": ""withheld resource"", ""Role"": ""storekeeper"", ""Occupations"": [""storekeeper""], ""FallbackTitle"": ""store delegate"",
      ""Props"": { ""prehistoric"": ""empty grain basket"", ""historic"": ""empty grain crate"" }, ""Silhouette"": ""wide open container beside the lead"",
      ""Lines"": {
        ""opening"": [""My {prop} is empty, and yet I hear grain is being hidden away. Help, {candidate}."", ""Share the harvest or the store runs dry. Will you speak up, {candidate}?""],
        ""progress"": [""A little grain came in. The store breathes again."", ""That helps. Keep the sacks coming.""],
        ""setback"": [""Someone took the sacks back. The store is bare."", ""Not enough. I am locking up for the day.""],
        ""revisit"": [""The {prop} still rattles, {candidate}."", ""We are down to crumbs. Will you try again?""],
        ""agreement"": [""The stores will be fair and full. {candidate}, you have my seal."", ""Everyone gets a share. I back you, {candidate}.""],
        ""acknowledgement"": [""The store is full, thanks to {winner}."", ""Nobody went hungry this winter because {winner} spoke.""]
      }
    },
    {
      ""Id"": ""tools"", ""Track"": 8, ""Pattern"": ""absent worker"", ""Role"": ""miner"", ""Occupations"": [""miner"", ""quarrier""], ""FallbackTitle"": ""diggers' delegate"",
      ""Props"": { ""prehistoric"": ""cracked stone pick"", ""historic"": ""cracked pickaxe"" }, ""Silhouette"": ""T-shaped haft with a heavy head"",
      ""Lines"": {
        ""opening"": [""My {prop} cracked and the diggers walked off. {candidate}, get me tools and hands."", ""Half my crew is gone. Can you bring them back, {candidate}?""],
        ""progress"": [""Two diggers came back. That helps."", ""A fresh handle at last. Go on.""],
        ""setback"": [""They walked off again. The rock stays where it is."", ""The haft broke once more. I am done today.""],
        ""revisit"": [""Still short of hands, {candidate}."", ""The {prop} waits for a new handle.""],
        ""agreement"": [""A new tool and a full crew. {candidate}, I am with you."", ""The work will start again. You have my vote, {candidate}.""],
        ""acknowledgement"": [""The digging goes on because {winner} stepped in."", ""My crew is back, and I thank {winner}.""]
      }
    },
    {
      ""Id"": ""shares"", ""Track"": 9, ""Pattern"": ""disputed ownership"", ""Role"": ""miller"", ""Occupations"": [""miller"", ""baker""], ""FallbackTitle"": ""grain delegate"",
      ""Props"": { ""prehistoric"": ""grain skin bag"", ""historic"": ""grain sack"" }, ""Silhouette"": ""slumped sack tied at the neck"",
      ""Lines"": {
        ""opening"": [""They say my {prop} is short. I say the share was never fair. Judge it, {candidate}."", ""Who owes whom a sack of grain? Settle it, {candidate}.""],
        ""progress"": [""We weighed it together. That is progress."", ""The numbers are closer, at least.""],
        ""setback"": [""They stormed off with the sack. No deal."", ""Too heavy a quarrel today. I am done.""],
        ""revisit"": [""The sack is still unsettled, {candidate}."", ""We are still arguing over weight, as you can see.""],
        ""agreement"": [""Fair weight, fair share. {candidate}, I back you."", ""The dispute ends here. Thank you, {candidate}.""],
        ""acknowledgement"": [""{winner} weighed it fair, and no one complains now."", ""The shares are even since {winner} stepped in.""]
      }
    },
    {
      ""Id"": ""water"", ""Track"": 10, ""Pattern"": ""withheld resource"", ""Role"": ""well keeper"", ""Occupations"": [""well keeper""], ""FallbackTitle"": ""water delegate"",
      ""Props"": { ""prehistoric"": ""bark bucket"", ""historic"": ""bucket"" }, ""Silhouette"": ""open vessel with a curved handle"",
      ""Lines"": {
        ""opening"": [""The well is low and tempers are high. Whose turn is it to draw, {candidate}?"", ""My {prop} comes up empty. Fix the water rota, {candidate}.""],
        ""progress"": [""A rota is chalked up. People are reading it."", ""There is a bit more water. Good.""],
        ""setback"": [""Fighting at the well again. I closed the lid."", ""Spilled and shouted over. Not today.""],
        ""revisit"": [""The well is still tense, {candidate}."", ""Same queue, same quarrels. Try again.""],
        ""agreement"": [""Everyone draws in turn. You have my vote, {candidate}."", ""The rota is fair. The well keeper is with you, {candidate}.""],
        ""acknowledgement"": [""The water queue runs smoothly. That is {winner}'s doing."", ""I fill my {prop} calmly now, thanks to {winner}.""]
      }
    },
    {
      ""Id"": ""witness"", ""Track"": 11, ""Pattern"": ""reluctant witness"", ""Role"": ""carter"", ""Occupations"": [""carter"", ""carrier""], ""FallbackTitle"": ""road delegate"",
      ""Props"": { ""prehistoric"": ""log roller"", ""historic"": ""cart wheel"" }, ""Silhouette"": ""round disc stood on its edge"",
      ""Lines"": {
        ""opening"": [""Someone saw who cracked my {prop}, but nobody will speak. Persuade them, {candidate}."", ""The witness is afraid to talk. Help me, {candidate}.""],
        ""progress"": [""The witness is thinking about it."", ""One more person says they saw something.""],
        ""setback"": [""The witness turned away and left. Back to nothing."", ""Door shut, mouths shut. Not today.""],
        ""revisit"": [""Still no witness, {candidate}. I understand if you are tired."", ""The {prop} stays broken until someone talks.""],
        ""agreement"": [""The witness spoke. {candidate}, thank you, I back you."", ""Now the truth is out. You have me, {candidate}.""],
        ""acknowledgement"": [""The truth came out because {winner} was patient."", ""Folk talk more openly now, thanks to {winner}.""]
      }
    },
    {
      ""Id"": ""deed"", ""Track"": 12, ""Pattern"": ""disputed ownership"", ""Role"": ""reeve"", ""Occupations"": [""reeve"", ""chief""], ""FallbackTitle"": ""elder delegate"",
      ""Props"": { ""prehistoric"": ""tally stick"", ""historic"": ""deed scroll"" }, ""Silhouette"": ""slim rolled bar held in one hand"",
      ""Lines"": {
        ""opening"": [""Two families claim the old plot, and my {prop} proves nothing. Help me, {candidate}."", ""I cannot rule on the {prop} alone, {candidate}. Will you help?""],
        ""progress"": [""Both sides agree to be heard."", ""A little closer to a ruling.""],
        ""setback"": [""The families walked out. The matter is open again."", ""No ruling today. I put the {prop} away.""],
        ""revisit"": [""The {prop} lies unread, {candidate}."", ""Still unsettled. Come back with something new.""],
        ""agreement"": [""A ruling at last, and it is fair. {candidate}, you have my seal."", ""Signed and witnessed. I am with you, {candidate}.""],
        ""acknowledgement"": [""The plot is settled, and {winner} helped to settle it."", ""{winner} brought both families to one table.""]
      }
    }
  ],
  ""ListeningLines"": [
    { ""Id"": ""c01"", ""Tag"": ""Complain"", ""Speaker"": ""Old fisher"", ""Text"": ""Every morning the path floods again, and nobody lifts a hand."" },
    { ""Id"": ""c02"", ""Tag"": ""Complain"", ""Speaker"": ""Young shepherd"", ""Text"": ""I mended that fence twice this moon and still the goats get through."" },
    { ""Id"": ""c03"", ""Tag"": ""Complain"", ""Speaker"": ""Basket weaver"", ""Text"": ""My back aches and my share is smaller than last month. Unfair."" },
    { ""Id"": ""c04"", ""Tag"": ""Complain"", ""Speaker"": ""Wool carder"", ""Text"": ""They talk and talk while the fire burns low. Nothing ever changes."" },
    { ""Id"": ""c05"", ""Tag"": ""Complain"", ""Speaker"": ""Stone knapper"", ""Text"": ""Somebody keeps borrowing my tools and never brings them back."" },
    { ""Id"": ""c06"", ""Tag"": ""Complain"", ""Speaker"": ""Hide tanner"", ""Text"": ""The smoke gets in my eyes every night. Why is it always my corner?"" },
    { ""Id"": ""c07"", ""Tag"": ""Complain"", ""Speaker"": ""Firewood hauler"", ""Text"": ""We carried all that water and got a thank-you from nobody."" },
    { ""Id"": ""c08"", ""Tag"": ""Complain"", ""Speaker"": ""Midwife"", ""Text"": ""It is too noisy by the stream, and the children never sleep."" },
    { ""Id"": ""c09"", ""Tag"": ""Complain"", ""Speaker"": ""Gleaner"", ""Text"": ""The berries were picked clean before I even woke. How is that fair?"" },
    { ""Id"": ""c10"", ""Tag"": ""Complain"", ""Speaker"": ""Salt trader"", ""Text"": ""I waited all day for the others and they never even came."" },
    { ""Id"": ""c11"", ""Tag"": ""Complain"", ""Speaker"": ""Potter"", ""Text"": ""My roof leaks and I asked for help three times already."" },
    { ""Id"": ""c12"", ""Tag"": ""Complain"", ""Speaker"": ""Bee keeper"", ""Text"": ""Why must we always walk the long way round the marsh?"" },
    { ""Id"": ""c13"", ""Tag"": ""Complain"", ""Speaker"": ""Net mender"", ""Text"": ""Half the nets were torn again. Someone is careless with them."" },
    { ""Id"": ""e01"", ""Tag"": ""Encourage"", ""Speaker"": ""Net mender"", ""Text"": ""You can do this. One more try and it will hold."" },
    { ""Id"": ""e02"", ""Tag"": ""Encourage"", ""Speaker"": ""Potter"", ""Text"": ""That was a fine knot. Keep your hands steady and go on."" },
    { ""Id"": ""e03"", ""Tag"": ""Encourage"", ""Speaker"": ""Old fisher"", ""Text"": ""Well done, friend. The hard part is already behind you."" },
    { ""Id"": ""e04"", ""Tag"": ""Encourage"", ""Speaker"": ""Young shepherd"", ""Text"": ""Do not stop now, the hill is nearly climbed."" },
    { ""Id"": ""e05"", ""Tag"": ""Encourage"", ""Speaker"": ""Midwife"", ""Text"": ""I believe in you. Take your time and show them."" },
    { ""Id"": ""e06"", ""Tag"": ""Encourage"", ""Speaker"": ""Hide tanner"", ""Text"": ""Everyone started clumsy once. Look how far you have come."" },
    { ""Id"": ""e07"", ""Tag"": ""Encourage"", ""Speaker"": ""Stone knapper"", ""Text"": ""Bravo! Your carving gets better every day."" },
    { ""Id"": ""e08"", ""Tag"": ""Encourage"", ""Speaker"": ""Bee keeper"", ""Text"": ""Stand tall. They will listen if you speak like that."" },
    { ""Id"": ""e09"", ""Tag"": ""Encourage"", ""Speaker"": ""Firewood hauler"", ""Text"": ""You are stronger than you think. Lift it together with me."" },
    { ""Id"": ""e10"", ""Tag"": ""Encourage"", ""Speaker"": ""Gleaner"", ""Text"": ""Good effort! The next one will be even better."" },
    { ""Id"": ""e11"", ""Tag"": ""Encourage"", ""Speaker"": ""Salt trader"", ""Text"": ""Keep going. The whole village is rooting for you."" },
    { ""Id"": ""e12"", ""Tag"": ""Encourage"", ""Speaker"": ""Basket weaver"", ""Text"": ""Nicely spoken. That took courage, and I noticed."" },
    { ""Id"": ""e13"", ""Tag"": ""Encourage"", ""Speaker"": ""Wool carder"", ""Text"": ""Try again, you were so close. I know you will get it."" },
    { ""Id"": ""w01"", ""Tag"": ""Warn"", ""Speaker"": ""Wool carder"", ""Text"": ""Careful with that ice. It cracks when you least expect it."" },
    { ""Id"": ""w02"", ""Tag"": ""Warn"", ""Speaker"": ""Old fisher"", ""Text"": ""Do not go near the marsh at dusk. The ground gives way."" },
    { ""Id"": ""w03"", ""Tag"": ""Warn"", ""Speaker"": ""Potter"", ""Text"": ""Watch the old beam, it is split and could fall on you."" },
    { ""Id"": ""w04"", ""Tag"": ""Warn"", ""Speaker"": ""Bee keeper"", ""Text"": ""Keep the fire away from the thatch or we will lose it all."" },
    { ""Id"": ""w05"", ""Tag"": ""Warn"", ""Speaker"": ""Net mender"", ""Text"": ""Mind the river, it rises fast after rain."" },
    { ""Id"": ""w06"", ""Tag"": ""Warn"", ""Speaker"": ""Young shepherd"", ""Text"": ""Wolves were seen near the ridge. Stay together tonight."" },
    { ""Id"": ""w07"", ""Tag"": ""Warn"", ""Speaker"": ""Midwife"", ""Text"": ""Those berries are bitter and make you sick. Leave them be."" },
    { ""Id"": ""w08"", ""Tag"": ""Warn"", ""Speaker"": ""Salt trader"", ""Text"": ""Do not trust a bargain that sounds too good to be true."" },
    { ""Id"": ""w09"", ""Tag"": ""Warn"", ""Speaker"": ""Hide tanner"", ""Text"": ""The storm is coming. Tie down everything you value."" },
    { ""Id"": ""w10"", ""Tag"": ""Warn"", ""Speaker"": ""Gleaner"", ""Text"": ""Do not lean on that rail, the rope is rotten."" },
    { ""Id"": ""w11"", ""Tag"": ""Warn"", ""Speaker"": ""Stone knapper"", ""Text"": ""Beware of the dry reeds, one spark and they all go up."" },
    { ""Id"": ""w12"", ""Tag"": ""Warn"", ""Speaker"": ""Firewood hauler"", ""Text"": ""Hold the bucket firmly, the well rope is slippery."" },
    { ""Id"": ""w13"", ""Tag"": ""Warn"", ""Speaker"": ""Basket weaver"", ""Text"": ""Take care crossing the log. It is wet and rolls."" },
    { ""Id"": ""r01"", ""Tag"": ""Reassure"", ""Speaker"": ""Basket weaver"", ""Text"": ""Do not worry, the grain will last until the harvest."" },
    { ""Id"": ""r02"", ""Tag"": ""Reassure"", ""Speaker"": ""Gleaner"", ""Text"": ""It is all right. Nobody blames you for the spilled water."" },
    { ""Id"": ""r03"", ""Tag"": ""Reassure"", ""Speaker"": ""Wool carder"", ""Text"": ""There is plenty for everyone, I promise."" },
    { ""Id"": ""r04"", ""Tag"": ""Reassure"", ""Speaker"": ""Old fisher"", ""Text"": ""Relax, the storm has passed and the roof held."" },
    { ""Id"": ""r05"", ""Tag"": ""Reassure"", ""Speaker"": ""Potter"", ""Text"": ""No harm done. We can fix it together by morning."" },
    { ""Id"": ""r06"", ""Tag"": ""Reassure"", ""Speaker"": ""Net mender"", ""Text"": ""The child is safe and sound, sleeping by the fire."" },
    { ""Id"": ""r07"", ""Tag"": ""Reassure"", ""Speaker"": ""Young shepherd"", ""Text"": ""You are not alone in this. We will manage together."" },
    { ""Id"": ""r08"", ""Tag"": ""Reassure"", ""Speaker"": ""Midwife"", ""Text"": ""It will heal, the wound is clean and shallow."" },
    { ""Id"": ""r09"", ""Tag"": ""Reassure"", ""Speaker"": ""Bee keeper"", ""Text"": ""Everything is in hand. You can rest now."" },
    { ""Id"": ""r10"", ""Tag"": ""Reassure"", ""Speaker"": ""Salt trader"", ""Text"": ""Do not fret, the others will come before the first frost."" },
    { ""Id"": ""r11"", ""Tag"": ""Reassure"", ""Speaker"": ""Hide tanner"", ""Text"": ""The fire is out and the camp is safe."" },
    { ""Id"": ""r12"", ""Tag"": ""Reassure"", ""Speaker"": ""Stone knapper"", ""Text"": ""Calm yourself. The lost goat is wandering back already."" },
    { ""Id"": ""r13"", ""Tag"": ""Reassure"", ""Speaker"": ""Firewood hauler"", ""Text"": ""It looks worse than it is. A little clay will mend it."" },
    { ""Id"": ""q01"", ""Tag"": ""Question"", ""Speaker"": ""Firewood hauler"", ""Text"": ""Where did you leave the good flint knife?"" },
    { ""Id"": ""q02"", ""Tag"": ""Question"", ""Speaker"": ""Basket weaver"", ""Text"": ""Who is going to cut the reeds this season?"" },
    { ""Id"": ""q03"", ""Tag"": ""Question"", ""Speaker"": ""Hide tanner"", ""Text"": ""Has anyone seen the traders on the river road?"" },
    { ""Id"": ""q04"", ""Tag"": ""Question"", ""Speaker"": ""Gleaner"", ""Text"": ""How much grain is left in the store?"" },
    { ""Id"": ""q05"", ""Tag"": ""Question"", ""Speaker"": ""Stone knapper"", ""Text"": ""Why did the elders move the boundary stones?"" },
    { ""Id"": ""q06"", ""Tag"": ""Question"", ""Speaker"": ""Net mender"", ""Text"": ""When does the crossing open again?"" },
    { ""Id"": ""q07"", ""Tag"": ""Question"", ""Speaker"": ""Wool carder"", ""Text"": ""Which path leads to the high pasture?"" },
    { ""Id"": ""q08"", ""Tag"": ""Question"", ""Speaker"": ""Old fisher"", ""Text"": ""Do you know who borrowed the big pot?"" },
    { ""Id"": ""q09"", ""Tag"": ""Question"", ""Speaker"": ""Potter"", ""Text"": ""What did the healer say about the fever?"" },
    { ""Id"": ""q10"", ""Tag"": ""Question"", ""Speaker"": ""Young shepherd"", ""Text"": ""Are we sharing the catch or trading it?"" },
    { ""Id"": ""q11"", ""Tag"": ""Question"", ""Speaker"": ""Midwife"", ""Text"": ""Can anyone tell me how deep the well is?"" },
    { ""Id"": ""q12"", ""Tag"": ""Question"", ""Speaker"": ""Salt trader"", ""Text"": ""Did you hear the horn from the hill last night?"" },
    { ""Id"": ""q13"", ""Tag"": ""Question"", ""Speaker"": ""Bee keeper"", ""Text"": ""Whose turn is it to mind the fire?"" }
  ]
}
";
    }
}
