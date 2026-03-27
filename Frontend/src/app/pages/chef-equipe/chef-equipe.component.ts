import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';
 

@Component({
  selector: 'app-chef-equipe',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './chef-equipe.component.html',
  styleUrl: './chef-equipe.component.scss'
})
export class ChefEquipeComponent implements OnInit {

  projects: { id: string, name: string }[] = [];
  selectedProjectId = '';
  steps: any[] = [];        // steps existants du backend (avec vrais ids)
  pendingSteps: any[] = []; // nouveaux steps pas encore soumis
  successMessage = '';
  errorMessage = '';
  loading = false;

  newStep = {
    stepName: '',
    toolName: '',
    order: 1,
    canBeParallel: false,
    dependsOnStepId: null as string | null
  };

  tools = ['axeIAM', 'axeBPM', 'axeGUI'];

  constructor(private http: HttpClient) {}

  ngOnInit(): void {
    this.http.get<any[]>(`${environment.apiUrl}/teams/my-chef-equipe-projects`).subscribe({
      next: (projects) => {
        this.projects = projects.map(p => ({ id: p.id, name: p.name }));
      }
    });
  }

  onProjectChange(): void {
    if (!this.selectedProjectId) return;
    this.pendingSteps = [];
    this.http.get<any[]>(`${environment.apiUrl}/steps/project/${this.selectedProjectId}`).subscribe({
      next: (steps) => this.steps = steps
    });
  }

  addStep(): void {
    if (!this.newStep.stepName || !this.newStep.toolName) {
      this.errorMessage = 'StepName et ToolName sont obligatoires.';
      return;
    }
    this.pendingSteps.push({ ...this.newStep });
    this.newStep = { stepName: '', toolName: '', order: this.pendingSteps.length + 1, canBeParallel: false, dependsOnStepId: null };
    this.errorMessage = '';
  }

  removeStep(index: number): void {
    this.pendingSteps.splice(index, 1);
  }

  submitSteps(): void {
    if (!this.selectedProjectId) {
      this.errorMessage = 'Sélectionnez un projet.';
      return;
    }
    if (this.pendingSteps.length === 0) {
      this.errorMessage = 'Ajoutez au moins un step.';
      return;
    }
    const body = {
    projectId: this.selectedProjectId,
    steps: this.pendingSteps.map(s => ({
      ...s,
      dependsOnStepId: s.dependsOnStepId || null
    }))
  };
    console.log('Body envoyé:', JSON.stringify(body));

    this.loading = true;
    this.successMessage = '';
    this.errorMessage = '';

    this.http.post(`${environment.apiUrl}/steps`, {
      projectId: this.selectedProjectId,
      steps: this.pendingSteps.map(s => ({
        ...s,
        dependsOnStepId: s.dependsOnStepId || null
      }))
    }).subscribe({
      next: () => {
        this.successMessage = 'Steps créés !';
        this.loading = false;
        this.pendingSteps = [];
        // Recharge les steps existants
        this.onProjectChange();
      },
      error: (err) => {
      console.log('Erreur détaillée:', err.error); // ← et ça
      this.errorMessage = 'Erreur: ' + JSON.stringify(err.error);
      this.loading = false;
    }
    });
  }
}