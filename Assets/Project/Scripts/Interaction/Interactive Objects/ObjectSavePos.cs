using UnityEngine;

namespace Project.Scripts.Interaction.Interactive_Objects
{
    public class ObjectSavePos : MonoBehaviour
    {
        [SerializeField] private GameObject objectToSave;
        private Vector3  _startPosition;
        private Rigidbody _rigidbody;

        public void Start()
        {
            _startPosition = objectToSave.transform.position;
            _rigidbody = objectToSave.GetComponent<Rigidbody>();
        }

        public void ReturnObject()
        {
            _rigidbody.linearVelocity = Vector3.zero;
            _rigidbody.angularVelocity = Vector3.zero;
            objectToSave.transform.position = _startPosition;
            _rigidbody.isKinematic = true;
        }
    }
}