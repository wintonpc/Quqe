- general workflow
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
  - Evaluate() against validation set instead of training set.
  - remove {Rnn,Rbf}TrainRecInfo. return the TrainRec directly
  - pick better initial weights: http://www.heatonresearch.com/encog/articles/nguyen-widrow-neural-network-weight.html
  - historical data in mongo
  - distributed training
  - distributed/parallel evaluation?
  - web UI
  - backtesting
  - catch NaN bug
  
NEXT: implement mutate
