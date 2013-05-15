- select ProtoRun from database
- store Run with chosen ProtoChromosome
- store generation 0
- randomly initialize and train generation 0
- train next generation
  - select, combine, and mutate
  - enqueue train requests containing:
    - generation ID
    - chromosome
  - collect train notifications
  - evaluate generation
    - evaluate Mixtures
    - attach MixtureEvals
  - attach GenEval
  
- request workers take a Chromosome as input
  - (an expert is the training output in addition to aux training input (e.g., initial weights)
    and metadata like final cost, cost over time during training, etc.)
  - train expert
    - preprocess data
    - train neural net
  - store Expert
  - broadcast training notification
  
  
- TODO
  - finish implementing Evolve() and helpers
  
NEXT: 