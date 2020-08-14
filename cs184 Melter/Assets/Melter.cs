using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

// General melting script.Currently configured to melt a lattice cube from its bottom left corner.
public class Melter : MonoBehaviour
{

    // Lattice structure definitions
    public float width = 20f, height = 20f, depth = 20f;
    public int num_w = 6, num_h = 6, num_d = 6;

    // Maximum spring constant
    public float SPRING_CONSTANT = 1000;

    // Amount of vibration. When amount = 0, no vibration occurs.
    public float VIBRATION = 50;

    // Seconds to wait before the simulation starts
    public float startTime = 3f;

    // Timestep between calculations for this simulation. Smaller timestep means more accuracy at higher performance cost.
    public float timeStep = 0.05f;

    // More timing variables.
    [HideInInspector] private float timer = 0.0f;
    [HideInInspector] private bool start = false;

    // Array of all the point masses in the scene.
    [HideInInspector] private GameObject[, ,] spheres;

    // List of all the springs in the scene.
    [HideInInspector] private List<SpringJoint> springs;

    // List of all the spring coefficients in the scene. We keep track of the original constants this way.
    [HideInInspector] private List<float> springFactors;

    // A gameobject for creating spheres.
    [HideInInspector] private GameObject s;


    // Use this for initialization
    void Start () {

    	print("Melting demo.");

    	float w_step = width / (num_w - 1);
    	float h_step = height / (num_h - 1);
    	float d_step = depth / (num_d - 1);

      spheres  = new GameObject[num_w, num_h, num_d];

      // Initialize spheres.
      for (int i = 0; i < num_w; i++) {
        for (int j = 0; j < num_h; j++) {
          for (int k = 0; k < num_d; k++) {

            s = GameObject.CreatePrimitive(PrimitiveType.Sphere);

            Vector3 new_position = GetComponent<Transform>().position + new Vector3(i * w_step, j * h_step, k * d_step);
            s.transform.position = new_position;
            s.transform.localScale += new Vector3 (2f, 2f, 2f);

            MeshRenderer mr = s.GetComponent<MeshRenderer>();
            Material[] mats = mr.materials;
            mats[0] = (Material) Resources.Load("pattern 27/Metal pattern 27", typeof(Material)) as Material;
            mats[0].shader = Shader.Find("Particles/Standard Surface");
            mats[0].EnableKeyword("_EMISSION");
            mr.materials = mats;

            Rigidbody r = s.AddComponent(typeof(Rigidbody)) as Rigidbody;
            r.mass = 20;
            SphereTemp t = s.AddComponent(typeof(SphereTemp)) as SphereTemp;

            t.temp = 0;
            t.rate_change = 0;
            r.useGravity = true;
            spheres[i,j,k] = s;
          }
        }
      }


      // Initialize springs.
      springs = new List<SpringJoint>();
      springFactors = new List<float>();
      for (int i = 0; i < num_w; i++) {
        for (int j = 0; j < num_h; j++) {
          for (int k = 0; k < num_d; k++) {
            s = spheres[i,j,k];

            for (int u = -1; u <= 1; u++) {
            	for (int v = -1; v <= 1; v++) {
            		for (int w = -1; w <= 1; w++) {
            			float divide = Math.Abs(u) + Math.Abs(v) + Math.Abs(w);
            			if (divide == 0) {
            				continue;
            			}
            			if (inRange(i+u, j+v, k+w)) {
            				SpringJoint sj = s.AddComponent(typeof(SpringJoint)) as SpringJoint;
            				// SpringFactor sf = sj.AddComponent(typeof(SpringFactor)) as SpringFactor;
			              sj.connectedBody = spheres[i+u, j+v, k+w].GetComponent<Rigidbody>();
			              sj.spring = SPRING_CONSTANT / divide;
			              // sf.spring_original = SPRING_CONSTANT / divide;
			              springs.Add(sj);
			              springFactors.Add(divide);
            			}
            		}
            	}
            }
          }
        }
      }

      // Set initial fixed point and update color.
      spheres[0,0,0].GetComponent<SphereTemp>().temp = 100f;
      updateColors();
    }


    // Update is called once per frame
    void Update()
    {
      timer += Time.deltaTime;
      if (!start && timer > startTime) {
        start = true;
        timer = 0.0f;
      }

      if (start && timer > timeStep) {

        timer = timer - timeStep;

        updateTemps();
        updateSprings();
        vibrateSpheres();
        updateColors();
      }
    }


    // Update temperature of each point mass.
    void updateTemps() {

      // Calculate temperature updates and store them in the rate_change attribute of SphereTemp component.
    	for (int i = 0; i < num_w; i++) {
    		for (int j = 0; j < num_h; j++) {
    			for (int k = 0; k < num_d; k++) {

    				GameObject m1 = spheres[i,j,k];
    				SphereTemp t1 = m1.GetComponent<SphereTemp>();

		   			float weightedTemps = 0;
					  float distanceSum = 0;

    				for (int u = -1; u <= 1; u++) {
              for (int v = -1; v <= 1; v++) {
                for (int w = -1; w <= 1; w++) {
            			if (u == 0 && v == 0 && w == 0) {
            				continue;
            			}

            			/* HEAT EQUATION
            			 * The rate of temperature change is proportional to how much hotter or cooler the surrounding material is.
            			 * 		1. Calculate weighted average of temperatures.
            			 *  	2. Set direct proportion to rate of temperature change.
            			 * 		3. Lerp it.
            			 */

            			if (inRange(i+u, j+v, k+w)) {
            				GameObject m2 = spheres[i+u, j+v, k+w];
            				SphereTemp t2 = m2.GetComponent<SphereTemp>();
            				float weight = 1f / (m1.transform.position - m2.transform.position).sqrMagnitude;
            				distanceSum += weight;
            				weightedTemps += weight * t2.temp;

            			}
            		}
            	}
            }
            float weightedAvgTemp = weightedTemps / distanceSum;
	    			float deltaTemp = weightedAvgTemp - t1.temp;
	    			t1.rate_change = deltaTemp * 10;
	    			t1.weighted_avg_temp = weightedAvgTemp;
    			}
    		}
    	}

      // Apply temperature updates to the current temp of each sphere.
    	for (int i = 0; i < num_w; i++) {
    		for (int j = 0; j < num_h; j++) {
    			for (int k = 0; k < num_d; k++) {
    				SphereTemp t = spheres[i,j,k].GetComponent<SphereTemp>();
    				t.temp += t.rate_change * timeStep;
    			}
    		}
    	}

      // Update fixed point.
      spheres[0,0,0].GetComponent<SphereTemp>().temp = 100f;

    }


    // Update spring constants.
    void updateSprings() {
    	for (var i = 0; i < springs.Count; i++) {
    		SpringJoint sj = springs[i];
    		float t1 = sj.gameObject.GetComponent<SphereTemp>().temp;
    		float t2 = sj.connectedBody.gameObject.GetComponent<SphereTemp>().temp;
    		float factor = getSpringFactor(t1, t2);
    		sj.spring = factor * SPRING_CONSTANT / springFactors[i];
    	}
    }


    // Update colors using an HSV lerp.
    void updateColors() {
    	for (int i = 0; i < num_w; i++) {
    		for (int j = 0; j < num_h; j++) {
    			for (int k = 0; k < num_d; k++) {
    				GameObject curr = spheres[i,j,k];
    				Color cold = Color.blue;
    				Color hot = Color.red;
    				float t = curr.GetComponent<SphereTemp>().temp / 100f;
    				Color lerpColor = lerpHSV(cold, hot, t);
    				var sphereRenderer = curr.GetComponent<Renderer>();
            		sphereRenderer.material.SetColor("_Color", lerpColor);
                sphereRenderer.material.SetColor("_EmissionColor", lerpColor);
    			}
    		}
    	}
    }


    // Calculate spring constant as a function of the temperatures of the point masses on both ends of the spring.
    float getSpringFactor(float temp1, float temp2) {
			float averageTemp = (temp1 + temp2) / 2f;
			float normalTemp = (averageTemp / 100f) * 500f + 200f;
			float elgiloyConstant = 0.6189531f -  0.0005764074f * normalTemp +  0.000001876103f * (float) Math.Pow(normalTemp, 2) - 2.064915e-9f * (float) Math.Pow(normalTemp, 3);
			return (elgiloyConstant - 0.4264926f) / 0.12f;
		}


    // Vibrate spheres to induce melting effect.
  	void vibrateSpheres() {
  		for (int i = 0; i < num_w; i++) {
  			for (int j = 0; j < num_h; j++) {
  				for (int k = 0; k < num_d; k++) {
  					GameObject curr = spheres[i,j,k];
  					Rigidbody r = curr.GetComponent<Rigidbody>();
  					Vector3 randomForce = UnityEngine.Random.insideUnitSphere;
  					r.AddForce(VIBRATION * randomForce);
					}
				}
			}
		}


    // Check whether [i,j,k] is an index within the spheres array.
    bool inRange(int i, int j, int k) {
      if (i < 0 || i >= num_w || j < 0 || j >= num_h || k < 0 || k >= num_d) {
        return false;
      }
      return true;
    }


    // Lerp in HSV space.
    Color lerpHSV(Color cold, Color hot, float t) {
    	float coldH, coldS, coldV;
    	float hotH, hotS, hotV;

    	Color.RGBToHSV(cold, out coldH, out coldS, out coldV);
    	Color.RGBToHSV(hot, out hotH, out hotS, out hotV);

    	float lerpH, lerpS, lerpV;
    	lerpH = Mathf.Lerp(coldH, hotH, t);
    	lerpS = Mathf.Lerp(coldS, hotS, t);
    	lerpV = Mathf.Lerp(coldV, hotV, t);


    	return Color.HSVToRGB(lerpH, lerpS, lerpV);
    }
}
