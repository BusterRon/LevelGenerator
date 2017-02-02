﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class ChunkInstantiator : ScriptableObject{

	//There may  still be dependencies to transforming objects. Therefore they are destroyed last.
	private ICollection<AbstractProperty> delayedRemovalCollection;
	private Stack<GameObject> workStack;
	//Differentiate between in editor preview and actual generation
	private ProcessType processType;
	private static ChunkInstantiator instance;

	private ChunkInstantiator(){

	}

	//1. Depth first search, traversing through the Object tree
	//2. Obtaining list from each GameObject with components implementing the AbstractProperty class
	//3. Sort the list depending on their priorities (Instantiating, Transforming, MeshGen)
	//4. Execute the components

	public void InstantiateChunk(GameObject chunk){
		Init ();
		workStack.Push (chunk);
		//chunk.tag = "Untagged";

		//Traversing, depth first
		while(workStack.Count > 0){
			GameObject currentObj = workStack.Pop ();
			ExecuteAbstractProperties (currentObj);
			PushChildrenToStack (currentObj);
		};

		if (processType == ProcessType.GENERATE) {
			CleanUp (); //Don't remove properties on preview
		}
	}

	//Used by the Level Generator in order to dictate the amount of doors for a chunk
	public void InstantiateChunk(GameObject chunk, int doorAmount){
		DoorManager doorManager = chunk.GetComponent<DoorManager> ();

		if (doorManager != null) {
			doorManager.FixedAmount = doorAmount;
		}

		InstantiateChunk (chunk);
	}

	private void Init(){
		this.delayedRemovalCollection = new List<AbstractProperty> ();
		this.workStack = new Stack<GameObject> ();
	}

	private void PushChildrenToStack(GameObject parent){		
		Stack<Transform> children = new Stack<Transform>();

		foreach (Transform t in parent.transform) {
			if (t.gameObject.activeSelf) {
				children.Push (t);
			}
		}

		while (children.Count > 0) {
			workStack.Push (children.Pop().gameObject);
		}
	}

	//Components implementing AbstractProperty are sorted by priority and then executed
	private void ExecuteAbstractProperties(GameObject obj){
		ICollection<AbstractProperty> properties = obj.GetComponents<AbstractProperty> ();

		if (properties.Count > 0) {
			properties = SortAbstractProperties (properties);
		}

		foreach (AbstractProperty property in properties) {
			if(!property.IsDirty){
				if (processType == ProcessType.GENERATE) {
					property.Generate ();
					HandleGeneratedObjects (property); //Add generated objs to work stack, if there are any
					HandlePropertyRemoval (property); //Remove component after execution
				} else {
					//No deletion or generated objects to handle in preview mode
					property.Preview ();
				}
			}
		}
	}

	//Arrays of the type InstantiatingProperty may generate Objects during generation time
	//They have to be added to the working stack in case they inherit abstract properties
	private void HandleGeneratedObjects(AbstractProperty property){		
		if (property.GeneratedObjects != null && property.GeneratedObjects.Count > 0) {
			foreach (GameObject genObj in property.GeneratedObjects) {
				workStack.Push (genObj);
			}
		}
	}

	//For purposes of cleaning up all abstract properties need to be removed during or after the creation process
	//As there may be dependencies, the removal of several properties can be delayed until the end of the generation process
	private void HandlePropertyRemoval(AbstractProperty property){
		if (property.RemovalTime == RemovalTime.DELAYED ||
			property.RemovalTime == RemovalTime.NEVER && processType == ProcessType.PREVIEW) {
			property.IsDirty = true; //Set dirty to avoid another execution
			delayedRemovalCollection.Add (property);
		} else if (property.RemovalTime == RemovalTime.INSTANTLY) {
			DestroyImmediate (property);
		}
	}

	//Sorts the Properties regarding to priority
	private ICollection<AbstractProperty> SortAbstractProperties(ICollection<AbstractProperty> properties){
		return properties.OrderBy (obj => obj.ExecutionOrder).ToList();
	}

	//Remove all components with delayed removal
	private void CleanUp(){
		foreach (AbstractProperty property in delayedRemovalCollection) {
			DestroyImmediate (property);
		}
	}

	public ProcessType ProcessType {
		get {
			return this.processType;
		}
		set {
			processType = value;
		}
	}

	public static ChunkInstantiator Instance{
		get{
			if(instance == null){
				instance = ScriptableObject.CreateInstance<ChunkInstantiator> ();
			}
			return instance;
		}
	}
}