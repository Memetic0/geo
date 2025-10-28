import { bindable, customElement } from 'aurelia';

interface NewIncident {
  type: string;
  latitude: number;
  longitude: number;
  severity: string;
  sensorStationId: string;
}

interface IncidentSummary {
  id: string;
  severity: string;
}

@customElement('incident-modals')
export class IncidentModals {
  // Create Modal
  @bindable showCreateModal: boolean = false;
  @bindable newIncident: NewIncident = {
    type: 'TrafficCongestion',
    latitude: 0,
    longitude: 0,
    severity: 'Moderate',
    sensorStationId: 'SENS-001'
  };
  @bindable onCreateIncident?: () => void;
  @bindable onCloseCreateModal?: () => void;

  // Assign Modal
  @bindable showAssignModal: boolean = false;
  @bindable assigningIncident: IncidentSummary | null = null;
  @bindable selectedResponderId: string = '';
  @bindable availableResponders: string[] = [];
  @bindable onAssignResponder?: () => void;
  @bindable onCloseAssignModal?: () => void;

  // Severity Modal
  @bindable showSeverityModal: boolean = false;
  @bindable changingSeverityIncident: IncidentSummary | null = null;
  @bindable newSeverity: string = '';
  @bindable onChangeSeverity?: () => void;
  @bindable onCloseSeverityModal?: () => void;

  // Create Modal Methods
  createIncident() {
    this.onCreateIncident?.();
  }

  closeCreateModal() {
    this.onCloseCreateModal?.();
  }

  // Assign Modal Methods
  confirmAssignResponder() {
    this.onAssignResponder?.();
  }

  closeAssignModal() {
    this.onCloseAssignModal?.();
  }

  // Severity Modal Methods
  confirmChangeSeverity() {
    this.onChangeSeverity?.();
  }

  closeSeverityModal() {
    this.onCloseSeverityModal?.();
  }
}
