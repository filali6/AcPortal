import { ComponentFixture, TestBed } from '@angular/core/testing';

import { ChefEquipeComponent } from './chef-equipe.component';

describe('ChefEquipeComponent', () => {
  let component: ChefEquipeComponent;
  let fixture: ComponentFixture<ChefEquipeComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ChefEquipeComponent]
    })
    .compileComponents();

    fixture = TestBed.createComponent(ChefEquipeComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
